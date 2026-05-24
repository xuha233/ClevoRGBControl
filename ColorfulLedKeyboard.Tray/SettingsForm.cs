using ColorfulLedKeyboard.Core;
using System.Diagnostics;

namespace ColorfulLedKeyboard.Tray;

public sealed class SettingsForm : Form
{
    private readonly SettingsStore _settingsStore;
    private readonly ComboBox _effectType = new();
    private readonly SliderRow _brightness = new("全局亮度", 0, 100, "%");
    private readonly ComboBox _speed = new();
    private readonly ColorPickerRow _effectColor = new("效果颜色");
    private readonly SliderRow _period = new("呼吸周期", 300, 30000, " ms");
    private readonly SliderRow _minimumBrightness = new("最低亮度", 0, 100, "%");
    private readonly CheckBox _hardBlink = new() { Text = "硬闪烁" };
    private readonly SequenceEditor _sequence = new();
    private readonly CheckBox _musicLevelColor = new() { Text = "按电平变色" };
    private readonly ColorPickerRow _musicLowColor = new("低电平颜色");
    private readonly ColorPickerRow _musicHighColor = new("高电平颜色");
    private readonly ComboBox _musicSensitivity = new();
    private readonly ComboBox _musicAttack = new();
    private readonly ComboBox _musicRelease = new();
    private readonly SliderRow _musicBaseBrightness = new("基础亮度", 0, 100, "%");
    private readonly SliderRow _musicPeakBrightness = new("峰值亮度", 0, 100, "%");
    private readonly CheckBox _idleEnabled = new() { Text = "启用空闲降亮" };
    private readonly ComboBox _idleAfter = new();
    private readonly SliderRow _idleBrightness = new("空闲亮度", 0, 100, "%");
    private readonly CheckBox _idleTurnOff = new() { Text = "空闲后关闭灯效" };
    private readonly CheckBox _scheduleEnabled = new() { Text = "启用时间计划" };
    private readonly TimeRangePicker _evening = new("傍晚时段");
    private readonly TimeRangePicker _night = new("深夜时段");
    private static readonly double[] MusicSensitivityValues = [0.5, 1.0, 1.5, 2.0];
    private static readonly int[] MusicAttackValues = [10, 35, 100, 300, 1000];
    private static readonly int[] MusicReleaseValues = [80, 180, 500, 1000, 3000];

    public SettingsForm(SettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        Text = "ClevoRGBControl 设置";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 560);
        ClientSize = new Size(780, 620);

        BuildUi();
        LoadSettings();
    }

    private void BuildUi()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(10, 4)
        };

        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildEffectTab());
        tabs.TabPages.Add(BuildMusicTab());
        tabs.TabPages.Add(BuildAutomationTab());
        tabs.TabPages.Add(BuildAdvancedTab());

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            Height = 52,
            Padding = new Padding(0, 8, 14, 10)
        };

        var save = new Button { Text = "保存", Width = 92, Height = 30 };
        save.Click += (_, _) => SaveSettings();

        var cancel = new Button { Text = "取消", Width = 92, Height = 30 };
        cancel.Click += (_, _) => Close();

        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        Controls.Add(tabs);
        Controls.Add(buttons);
    }

    private TabPage BuildGeneralTab()
    {
        var (tab, page) = CreatePage("常规");
        _effectType.DropDownStyle = ComboBoxStyle.DropDownList;
        _effectType.Items.AddRange(["固定颜色", "RGB 循环", "单色呼吸", "色彩序列", "音乐模式", "关闭"]);

        _speed.DropDownStyle = ComboBoxStyle.DropDownList;
        _speed.Items.AddRange(["非常慢", "慢", "正常", "快", "很快"]);

        page.Controls.Add(Row("当前效果", _effectType));
        page.Controls.Add(_brightness);
        page.Controls.Add(Row("速度", _speed));
        return tab;
    }

    private TabPage BuildEffectTab()
    {
        var (tab, page) = CreatePage("效果");
        page.Controls.Add(_effectColor);
        page.Controls.Add(_period);
        page.Controls.Add(_minimumBrightness);
        page.Controls.Add(PlainRow(_hardBlink));
        page.Controls.Add(Section("色彩序列"));
        page.Controls.Add(_sequence);
        return tab;
    }

    private TabPage BuildMusicTab()
    {
        var (tab, page) = CreatePage("音乐");
        SetupCombo(_musicSensitivity, MusicSensitivityValues.Select(value => $"{value:0.0}x"));
        SetupCombo(_musicAttack, MusicAttackValues.Select(value => $"{value} ms"));
        SetupCombo(_musicRelease, MusicReleaseValues.Select(value => $"{value} ms"));

        page.Controls.Add(PlainRow(_musicLevelColor));
        page.Controls.Add(_musicLowColor);
        page.Controls.Add(_musicHighColor);
        page.Controls.Add(Row("灵敏度", _musicSensitivity));
        page.Controls.Add(Row("响应速度", _musicAttack));
        page.Controls.Add(Row("衰减速度", _musicRelease));
        page.Controls.Add(_musicBaseBrightness);
        page.Controls.Add(_musicPeakBrightness);
        return tab;
    }

    private TabPage BuildAutomationTab()
    {
        var (tab, page) = CreatePage("自动化");
        _idleAfter.DropDownStyle = ComboBoxStyle.DropDownList;
        _idleAfter.Items.AddRange(["1 分钟", "3 分钟", "5 分钟", "10 分钟", "30 分钟"]);

        page.Controls.Add(Section("空闲降亮"));
        page.Controls.Add(PlainRow(_idleEnabled));
        page.Controls.Add(Row("空闲时间", _idleAfter));
        page.Controls.Add(_idleBrightness);
        page.Controls.Add(PlainRow(_idleTurnOff));
        page.Controls.Add(Section("时间计划"));
        page.Controls.Add(PlainRow(_scheduleEnabled));
        page.Controls.Add(_evening);
        page.Controls.Add(_night);
        return tab;
    }

    private TabPage BuildAdvancedTab()
    {
        var (tab, page) = CreatePage("高级");

        var configPath = new TextBox
        {
            Text = AppPaths.SettingsPath,
            ReadOnly = true,
            Width = 430
        };

        var openFolder = new Button { Text = "打开配置目录", Width = 120 };
        openFolder.Click += (_, _) =>
        {
            Directory.CreateDirectory(AppPaths.ProgramDataDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.ProgramDataDirectory) { UseShellExecute = true });
        };

        var reset = new Button { Text = "恢复默认设置", Width = 120 };
        reset.Click += (_, _) =>
        {
            if (MessageBox.Show("确定恢复默认设置？", "ClevoRGBControl", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            _settingsStore.Save(new KeyboardSettings());
            LoadSettings();
        };

        page.Controls.Add(Row("配置文件", configPath));
        page.Controls.Add(PlainRow(openFolder));
        page.Controls.Add(PlainRow(reset));
        return tab;
    }

    private void LoadSettings()
    {
        var settings = _settingsStore.Load();
        _effectType.SelectedIndex = settings.Effect.Type switch
        {
            EffectType.Static => 0,
            EffectType.Rainbow => 1,
            EffectType.Breathing => 2,
            EffectType.Sequence => 3,
            EffectType.Music => 4,
            EffectType.Off => 5,
            _ => 1
        };

        _brightness.Value = settings.Brightness;
        _speed.SelectedIndex = SpeedToIndex(settings.Effect.Step, settings.Effect.IntervalMs);
        _effectColor.ColorHex = settings.Effect.Color;
        _period.Value = settings.Effect.PeriodMs;
        _minimumBrightness.Value = settings.Effect.MinimumBrightness;
        _hardBlink.Checked = settings.Effect.HardBlink;
        _sequence.Colors = settings.Effect.Sequence.Select(item => item.Color).ToList();
        _musicLevelColor.Checked = settings.Effect.Music.LevelColorEnabled;
        _musicLowColor.ColorHex = settings.Effect.Music.LowColor;
        _musicHighColor.ColorHex = settings.Effect.Music.HighColor;
        _musicSensitivity.SelectedIndex = ClosestIndex(MusicSensitivityValues, settings.Effect.Music.Sensitivity);
        _musicAttack.SelectedIndex = ClosestIndex(MusicAttackValues, settings.Effect.Music.AttackMs);
        _musicRelease.SelectedIndex = ClosestIndex(MusicReleaseValues, settings.Effect.Music.ReleaseMs);
        _musicBaseBrightness.Value = settings.Effect.Music.BaseBrightness;
        _musicPeakBrightness.Value = settings.Effect.Music.PeakBrightness;
        _idleEnabled.Checked = settings.IdleDim.Enabled;
        _idleAfter.SelectedIndex = SecondsToIdleIndex(settings.IdleDim.AfterSeconds);
        _idleBrightness.Value = settings.IdleDim.Brightness;
        _idleTurnOff.Checked = settings.IdleDim.TurnOff;
        _scheduleEnabled.Checked = settings.Schedule.Enabled;

        var evening = settings.Schedule.Rules.FirstOrDefault(rule => rule.Name == "Evening");
        var night = settings.Schedule.Rules.FirstOrDefault(rule => rule.Name == "Night");
        _evening.SetRange(evening?.Start ?? "19:00", evening?.End ?? "23:30");
        _night.SetRange(night?.Start ?? "23:30", night?.End ?? "07:00");
    }

    private void SaveSettings()
    {
        try
        {
            var settings = _settingsStore.Load();
            settings.Enabled = true;
            settings.Brightness = _brightness.Value;
            settings.Effect.Type = SelectedEffectType();
            settings.Effect.Color = _effectColor.ColorHex;
            ApplySpeed(settings);
            settings.Effect.PeriodMs = _period.Value;
            settings.Effect.MinimumBrightness = _minimumBrightness.Value;
            settings.Effect.HardBlink = _hardBlink.Checked;
            settings.Effect.Sequence = _sequence.Colors.Select(color => new SequenceColor
            {
                Color = color,
                HoldMs = 300,
                TransitionMs = 1200,
                Breathing = true
            }).ToList();

            settings.Effect.Music.LevelColorEnabled = _musicLevelColor.Checked;
            settings.Effect.Music.LowColor = _musicLowColor.ColorHex;
            settings.Effect.Music.HighColor = _musicHighColor.ColorHex;
            settings.Effect.Music.Sensitivity = MusicSensitivityValues[Math.Max(0, _musicSensitivity.SelectedIndex)];
            settings.Effect.Music.AttackMs = MusicAttackValues[Math.Max(0, _musicAttack.SelectedIndex)];
            settings.Effect.Music.ReleaseMs = MusicReleaseValues[Math.Max(0, _musicRelease.SelectedIndex)];
            settings.Effect.Music.BaseBrightness = _musicBaseBrightness.Value;
            settings.Effect.Music.PeakBrightness = _musicPeakBrightness.Value;
            settings.IdleDim.Enabled = _idleEnabled.Checked;
            settings.IdleDim.AfterSeconds = IdleIndexToSeconds(_idleAfter.SelectedIndex);
            settings.IdleDim.Brightness = _idleBrightness.Value;
            settings.IdleDim.TurnOff = _idleTurnOff.Checked;
            settings.Schedule.Enabled = _scheduleEnabled.Checked;
            settings.Schedule.Rules = BuildScheduleRules();
            _settingsStore.Save(settings);
            Text = "ClevoRGBControl 设置 - 已保存";
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"无法保存设置：{ex.Message}", "ClevoRGBControl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private List<ScheduleRule> BuildScheduleRules()
    {
        return
        [
            new ScheduleRule
            {
                Name = "Evening",
                Start = _evening.StartTime,
                End = _evening.EndTime,
                Enabled = true,
                Brightness = 35,
                Effect = new LightingEffectSettings { Type = EffectType.Static, Color = "#FFD2A1" }
            },
            new ScheduleRule
            {
                Name = "Night",
                Start = _night.StartTime,
                End = _night.EndTime,
                Enabled = true,
                Brightness = 0,
                Effect = new LightingEffectSettings { Type = EffectType.Off }
            }
        ];
    }

    private static (TabPage Tab, FlowLayoutPanel Page) CreatePage(string title)
    {
        var tab = new TabPage(title);
        var page = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(18, 18, 18, 28)
        };
        tab.Controls.Add(page);
        return (tab, page);
    }

    private static Panel Row(string label, Control control)
    {
        var panel = new Panel { Width = 680, Height = 42 };
        panel.Controls.Add(new Label { Text = label, Width = 140, Location = new Point(0, 9) });
        control.Location = new Point(150, 6);
        control.Width = Math.Max(control.Width, 220);
        panel.Controls.Add(control);
        return panel;
    }

    private static void SetupCombo(ComboBox combo, IEnumerable<string> values)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        if (combo.Items.Count == 0)
        {
            combo.Items.AddRange(values.Cast<object>().ToArray());
        }
    }

    private static Panel PlainRow(Control control)
    {
        var panel = new Panel { Width = 680, Height = 38 };
        control.Location = new Point(150, 7);
        panel.Controls.Add(control);
        return panel;
    }

    private static Label Section(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            Width = 680,
            Height = 30,
            Padding = new Padding(0, 8, 0, 0)
        };
    }

    private EffectType SelectedEffectType() => _effectType.SelectedIndex switch
    {
        0 => EffectType.Static,
        1 => EffectType.Rainbow,
        2 => EffectType.Breathing,
        3 => EffectType.Sequence,
        4 => EffectType.Music,
        5 => EffectType.Off,
        _ => EffectType.Rainbow
    };

    private void ApplySpeed(KeyboardSettings settings)
    {
        (settings.Effect.Step, settings.Effect.IntervalMs) = _speed.SelectedIndex switch
        {
            0 => (1, 160),
            1 => (1, 80),
            3 => (6, 30),
            4 => (10, 20),
            _ => (3, 40)
        };
    }

    private static int SpeedToIndex(int step, int intervalMs)
    {
        if (step <= 1 && intervalMs >= 120) return 0;
        if (step <= 1 || intervalMs >= 80) return 1;
        if (step >= 10 || intervalMs <= 20) return 4;
        if (step >= 6 || intervalMs <= 30) return 3;
        return 2;
    }

    private static int ClosestIndex(double[] values, double target)
    {
        var best = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < values.Length; i++)
        {
            var distance = Math.Abs(values[i] - target);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static int ClosestIndex(int[] values, int target)
    {
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < values.Length; i++)
        {
            var distance = Math.Abs(values[i] - target);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static int SecondsToIdleIndex(int seconds) => seconds switch
    {
        <= 60 => 0,
        <= 180 => 1,
        <= 300 => 2,
        <= 600 => 3,
        _ => 4
    };

    private static int IdleIndexToSeconds(int index) => index switch
    {
        0 => 60,
        1 => 180,
        2 => 300,
        3 => 600,
        _ => 1800
    };
}

internal sealed class SliderRow : UserControl
{
    private readonly TrackBar _track = new();
    private readonly Label _value = new();
    private readonly string _suffix;
    private readonly Func<int, string>? _formatter;

    public SliderRow(string label, int min, int max, string suffix, Func<int, string>? formatter = null)
    {
        _suffix = suffix;
        _formatter = formatter;
        Width = 680;
        Height = 54;

        Controls.Add(new Label { Text = label, Width = 140, Location = new Point(0, 17) });
        _track.Location = new Point(145, 8);
        _track.Width = 380;
        _track.Minimum = min;
        _track.Maximum = max;
        _track.TickFrequency = Math.Max(1, (max - min) / 10);
        _track.ValueChanged += (_, _) => UpdateLabel();
        Controls.Add(_track);

        _value.Location = new Point(535, 17);
        _value.Width = 100;
        Controls.Add(_value);
    }

    public int Value
    {
        get => _track.Value;
        set
        {
            _track.Value = Math.Clamp(value, _track.Minimum, _track.Maximum);
            UpdateLabel();
        }
    }

    private void UpdateLabel()
    {
        _value.Text = _formatter is null ? $"{_track.Value}{_suffix}" : $"{_formatter(_track.Value)}{_suffix}";
    }
}

internal sealed class ColorPickerRow : UserControl
{
    private readonly Panel _swatch = new();
    private readonly TextBox _hex = new();

    public ColorPickerRow(string label)
    {
        Width = 680;
        Height = 46;
        Controls.Add(new Label { Text = label, Width = 140, Location = new Point(0, 13) });

        _swatch.Location = new Point(150, 10);
        _swatch.Size = new Size(28, 24);
        _swatch.BorderStyle = BorderStyle.FixedSingle;
        _swatch.Click += (_, _) => PickColor();
        Controls.Add(_swatch);

        _hex.Location = new Point(190, 10);
        _hex.Width = 90;
        _hex.TextChanged += (_, _) => UpdateSwatch();
        Controls.Add(_hex);

        var pick = new Button { Text = "选择...", Location = new Point(290, 8), Width = 82 };
        pick.Click += (_, _) => PickColor();
        Controls.Add(pick);

        var palette = new[] { "#FF0000", "#FF8000", "#FFFF00", "#00FF00", "#00FFFF", "#0060FF", "#8000FF", "#FFFFFF", "#FFD2A1", "#CFE8FF" };
        var x = 386;
        foreach (var color in palette)
        {
            var button = new Button { BackColor = ColorTranslator.FromHtml(color), Location = new Point(x, 9), Size = new Size(24, 24), FlatStyle = FlatStyle.Flat };
            button.Click += (_, _) => ColorHex = color;
            Controls.Add(button);
            x += 28;
        }
    }

    public string ColorHex
    {
        get => RgbColor.FromHex(_hex.Text).Hex;
        set
        {
            _hex.Text = RgbColor.FromHex(value).Hex;
            UpdateSwatch();
        }
    }

    private void PickColor()
    {
        using var dialog = new ColorDialog { FullOpen = true, Color = _swatch.BackColor };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            ColorHex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void UpdateSwatch()
    {
        try
        {
            _swatch.BackColor = ColorTranslator.FromHtml(RgbColor.FromHex(_hex.Text).Hex);
            _hex.BackColor = SystemColors.Window;
        }
        catch (FormatException)
        {
            _hex.BackColor = Color.MistyRose;
        }
    }
}

internal sealed class TimeRangePicker : UserControl
{
    private readonly ComboBox _startHour = new();
    private readonly ComboBox _startMinute = new();
    private readonly ComboBox _endHour = new();
    private readonly ComboBox _endMinute = new();

    public TimeRangePicker(string label)
    {
        Width = 680;
        Height = 44;
        Controls.Add(new Label { Text = label, Width = 140, Location = new Point(0, 12) });
        Setup(_startHour, 150, Enumerable.Range(0, 24).Select(value => value.ToString("00")));
        Setup(_startMinute, 218, ["00", "15", "30", "45"]);
        Controls.Add(new Label { Text = "到", Location = new Point(286, 13), AutoSize = true });
        Setup(_endHour, 320, Enumerable.Range(0, 24).Select(value => value.ToString("00")));
        Setup(_endMinute, 388, ["00", "15", "30", "45"]);
    }

    public string StartTime => $"{_startHour.Text}:{_startMinute.Text}";

    public string EndTime => $"{_endHour.Text}:{_endMinute.Text}";

    public void SetRange(string start, string end)
    {
        SetTime(start, _startHour, _startMinute);
        SetTime(end, _endHour, _endMinute);
    }

    private void Setup(ComboBox combo, int x, IEnumerable<string> values)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Items.AddRange(values.Cast<object>().ToArray());
        combo.Location = new Point(x, 9);
        combo.Width = 58;
        Controls.Add(combo);
    }

    private static void SetTime(string value, ComboBox hour, ComboBox minute)
    {
        if (!TimeOnly.TryParse(value, out var time))
        {
            time = new TimeOnly(0, 0);
        }

        hour.Text = time.Hour.ToString("00");
        minute.Text = ((time.Minute / 15) * 15).ToString("00");
    }
}

internal sealed class SequenceEditor : UserControl
{
    private readonly ListBox _list = new();
    private readonly List<string> _colors = [];

    public SequenceEditor()
    {
        Width = 680;
        Height = 170;

        _list.Location = new Point(150, 4);
        _list.Size = new Size(250, 150);
        _list.DrawMode = DrawMode.OwnerDrawFixed;
        _list.ItemHeight = 24;
        _list.DrawItem += DrawItem;
        Controls.Add(_list);

        AddButton("添加", 420, 4, AddColor);
        AddButton("删除", 420, 40, RemoveSelected);
        AddButton("上移", 420, 76, () => MoveSelected(-1));
        AddButton("下移", 420, 112, () => MoveSelected(1));
    }

    public List<string> Colors
    {
        get => [.. _colors];
        set
        {
            _colors.Clear();
            _colors.AddRange(value.Select(color => RgbColor.FromHex(color).Hex));
            RefreshList();
        }
    }

    private void AddButton(string text, int x, int y, Action action)
    {
        var button = new Button { Text = text, Location = new Point(x, y), Width = 76 };
        button.Click += (_, _) => action();
        Controls.Add(button);
    }

    private void AddColor()
    {
        using var dialog = new ColorDialog { FullOpen = true };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _colors.Add($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
            RefreshList();
        }
    }

    private void RemoveSelected()
    {
        if (_list.SelectedIndex < 0)
        {
            return;
        }

        _colors.RemoveAt(_list.SelectedIndex);
        RefreshList();
    }

    private void MoveSelected(int direction)
    {
        var index = _list.SelectedIndex;
        var target = index + direction;
        if (index < 0 || target < 0 || target >= _colors.Count)
        {
            return;
        }

        (_colors[index], _colors[target]) = (_colors[target], _colors[index]);
        RefreshList();
        _list.SelectedIndex = target;
    }

    private void RefreshList()
    {
        _list.Items.Clear();
        foreach (var color in _colors)
        {
            _list.Items.Add(color);
        }
    }

    private void DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }

        e.DrawBackground();
        var color = ColorTranslator.FromHtml(_colors[e.Index]);
        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, e.Bounds.Left + 4, e.Bounds.Top + 4, 28, 16);
        e.Graphics.DrawRectangle(Pens.Black, e.Bounds.Left + 4, e.Bounds.Top + 4, 28, 16);
        e.Graphics.DrawString(_colors[e.Index], e.Font ?? Font, Brushes.Black, e.Bounds.Left + 40, e.Bounds.Top + 4);
        e.DrawFocusRectangle();
    }
}
