namespace ColorfulLedKeyboard.Service;

using ColorfulLedKeyboard.Core;
using System.Runtime.InteropServices;

public class Worker : BackgroundService
{
    private readonly SettingsStore _settingsStore = new();
    private readonly DchuKeyboardDevice _device = new();
    private readonly SystemAudioLevelMeter _audioLevelMeter = new();
    private readonly ILogger<Worker> _logger;
    private FileSystemWatcher? _watcher;
    private volatile bool _settingsChanged = true;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        EnsureConfigWatcher();
        await FlashStartupAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = BuildRuntimeSettings(_settingsStore.Load());
            _settingsChanged = false;

            if (!settings.Enabled)
            {
                await WaitForSettingsChangeAsync(1000, stoppingToken);
                continue;
            }

            try
            {
                await RunEffectAsync(settings, stoppingToken);
            }
            catch (DllNotFoundException ex)
            {
                _logger.LogError(ex, "InsydeDCHU.dll was not found. Copy it next to the service executable.");
                await Task.Delay(5000, stoppingToken);
            }
            catch (EntryPointNotFoundException ex)
            {
                _logger.LogError(ex, "InsydeDCHU.dll does not expose SetDCHU_Data.");
                await Task.Delay(5000, stoppingToken);
            }
            catch (SEHException ex)
            {
                _logger.LogError(ex, "The keyboard LED driver rejected the operation.");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _audioLevelMeter.Dispose();
        return base.StopAsync(cancellationToken);
    }

    private async Task RunEffectAsync(KeyboardSettings settings, CancellationToken stoppingToken)
    {
        if (settings.Effect.Type == EffectType.Music)
        {
            await RunMusicAsync(settings, stoppingToken);
            return;
        }

        var generator = new LightingFrameGenerator(settings);
        var nextRuntimeRefresh = DateTimeOffset.UtcNow.AddSeconds(1);
        RgbColor? lastColor = null;

        while (!stoppingToken.IsCancellationRequested && !_settingsChanged)
        {
            if (DateTimeOffset.UtcNow >= nextRuntimeRefresh)
            {
                nextRuntimeRefresh = DateTimeOffset.UtcNow.AddSeconds(1);
                if (ShouldRebuildRuntimeSettings(settings))
                {
                    _settingsChanged = true;
                    return;
                }
            }

            var color = generator.Next();
            if (color != lastColor)
            {
                _device.SetAllZones(color);
                lastColor = color;
            }

            if (settings.Effect.Type is EffectType.Static or EffectType.Off)
            {
                await WaitForSettingsChangeAsync(1000, stoppingToken);
                _settingsChanged = true;
                return;
            }

            await Task.Delay(generator.IntervalMs, stoppingToken);
        }
    }

    private async Task RunMusicAsync(KeyboardSettings settings, CancellationToken stoppingToken)
    {
        var music = settings.Effect.Music.Normalize();
        var controller = new MusicBrightnessController();
        var baseColor = ResolveMusicBaseColor(settings);
        var lowColor = RgbColor.FromHex(music.LowColor);
        var highColor = RgbColor.FromHex(music.HighColor);
        var nextRuntimeRefresh = DateTimeOffset.UtcNow.AddSeconds(1);
        RgbColor? lastColor = null;

        while (!stoppingToken.IsCancellationRequested && !_settingsChanged)
        {
            if (DateTimeOffset.UtcNow >= nextRuntimeRefresh)
            {
                nextRuntimeRefresh = DateTimeOffset.UtcNow.AddSeconds(1);
                if (ShouldRebuildRuntimeSettings(settings))
                {
                    _settingsChanged = true;
                    return;
                }
            }

            var level = _audioLevelMeter.GetPeakLevel();
            var brightness = controller.NextBrightness(music, level);
            var sourceColor = music.LevelColorEnabled
                ? RgbColor.Lerp(lowColor, highColor, Math.Clamp(level * music.Sensitivity, 0, 1))
                : baseColor;
            var color = sourceColor.Scale(brightness);

            if (color != lastColor)
            {
                _device.SetAllZones(color);
                lastColor = color;
            }

            await Task.Delay(music.IntervalMs, stoppingToken);
        }
    }

    private static RgbColor ResolveMusicBaseColor(KeyboardSettings settings)
    {
        if (settings.Effect.Type is EffectType.Static or EffectType.Breathing or EffectType.Music)
        {
            return RgbColor.FromHex(settings.Effect.Color);
        }

        return RgbColor.FromHex(settings.StaticColor);
    }

    private static KeyboardSettings BuildRuntimeSettings(KeyboardSettings settings)
    {
        var runtime = settings.CloneForRuntime();
        ApplySchedule(runtime);
        ApplyIdleDim(runtime);
        return runtime.Normalize();
    }

    private static bool ShouldRebuildRuntimeSettings(KeyboardSettings current)
    {
        var next = BuildRuntimeSettings(new SettingsStore().Load());
        return next.Enabled != current.Enabled ||
            next.Brightness != current.Brightness ||
            next.Effect.Type != current.Effect.Type ||
            next.Effect.Color != current.Effect.Color ||
            next.Effect.Step != current.Effect.Step ||
            next.Effect.IntervalMs != current.Effect.IntervalMs ||
            next.Effect.PeriodMs != current.Effect.PeriodMs ||
            next.Effect.MinimumBrightness != current.Effect.MinimumBrightness ||
            next.Effect.HardBlink != current.Effect.HardBlink ||
            next.Effect.Sequence.Count != current.Effect.Sequence.Count ||
            next.Effect.Sequence.Zip(current.Effect.Sequence).Any(pair =>
                pair.First.Color != pair.Second.Color ||
                pair.First.HoldMs != pair.Second.HoldMs ||
                pair.First.TransitionMs != pair.Second.TransitionMs ||
                pair.First.Breathing != pair.Second.Breathing);
    }

    private async Task FlashStartupAsync(CancellationToken stoppingToken)
    {
        try
        {
            for (var i = 0; i < 2; i++)
            {
                _device.SetAllZones(new RgbColor(255, 255, 255));
                await Task.Delay(120, stoppingToken);
                _device.SetAllZones(RgbColor.Black);
                await Task.Delay(120, stoppingToken);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or SEHException)
        {
            _logger.LogWarning(ex, "Startup flash could not be sent to the keyboard.");
        }
    }

    private static void ApplySchedule(KeyboardSettings settings)
    {
        if (!settings.Schedule.Enabled)
        {
            return;
        }

        var now = TimeOnly.FromDateTime(DateTime.Now);
        var rule = settings.Schedule.Rules.FirstOrDefault(item => item.Enabled && item.IsActive(now));
        if (rule is null)
        {
            return;
        }

        settings.Enabled = true;
        settings.Brightness = rule.Brightness;
        settings.Effect = rule.Effect;
    }

    private static void ApplyIdleDim(KeyboardSettings settings)
    {
        if (!settings.IdleDim.Enabled)
        {
            return;
        }

        if (WindowsIdleTime.GetIdleTime().TotalSeconds < settings.IdleDim.AfterSeconds)
        {
            return;
        }

        if (settings.IdleDim.TurnOff)
        {
            settings.Effect.Type = EffectType.Off;
            return;
        }

        settings.Brightness = Math.Min(settings.Brightness, settings.IdleDim.Brightness);
    }

    private void EnsureConfigWatcher()
    {
        Directory.CreateDirectory(AppPaths.ProgramDataDirectory);
        _watcher = new FileSystemWatcher(AppPaths.ProgramDataDirectory, AppPaths.SettingsFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _watcher.Changed += (_, _) => _settingsChanged = true;
        _watcher.Created += (_, _) => _settingsChanged = true;
        _watcher.Deleted += (_, _) => _settingsChanged = true;
        _watcher.Renamed += (_, _) => _settingsChanged = true;
    }

    private async Task WaitForSettingsChangeAsync(int pollIntervalMs, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && !_settingsChanged)
        {
            await Task.Delay(pollIntervalMs, stoppingToken);
        }
    }
}
