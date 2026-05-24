namespace ColorfulLedKeyboard.Core;

public sealed class MusicSettings
{
    public bool LevelColorEnabled { get; set; }

    public string LowColor { get; set; } = "#0040FF";

    public string HighColor { get; set; } = "#FF0040";

    public double Sensitivity { get; set; } = 1.5;

    public int AttackMs { get; set; } = 35;

    public int ReleaseMs { get; set; } = 180;

    public int BaseBrightness { get; set; } = 5;

    public int PeakBrightness { get; set; } = 100;

    public int IntervalMs { get; set; } = 25;

    public MusicSettings Normalize()
    {
        LowColor = LightingEffectSettings.NormalizeHex(LowColor, "#0040FF");
        HighColor = LightingEffectSettings.NormalizeHex(HighColor, "#FF0040");
        Sensitivity = Math.Clamp(Sensitivity, 0.5, 2.0);
        AttackMs = Math.Clamp(AttackMs, 10, 1000);
        ReleaseMs = Math.Clamp(ReleaseMs, 20, 3000);
        BaseBrightness = Math.Clamp(BaseBrightness, 0, 100);
        PeakBrightness = Math.Clamp(PeakBrightness, BaseBrightness, 100);
        IntervalMs = Math.Clamp(IntervalMs, 15, 200);
        return this;
    }
}
