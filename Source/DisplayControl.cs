using System;
using System.Collections.Generic;

namespace ScreenGlass;

public sealed class DisplayProfile
{
    public bool Enabled { get; set; } = true;
    public double Dimming { get; set; }
    public bool Warm { get; set; }
    public bool SoftLight { get; set; } = true;
}

public sealed record DisplayInfo(string Id, string Label, System.Drawing.Rectangle Bounds)
{
    public static IReadOnlyList<DisplayInfo> Detect() => NativeDisplays.Detect();
}

public static class DimmingMath
{
    // Alpha composition retains fixed dimming even when adaptive shade is zero.
    public static double Combine(double dimming, double adaptive) =>
        Math.Min(0.9, 1 - (1 - Math.Clamp(dimming, 0, 90) / 100) * (1 - Math.Clamp(adaptive, 0, 0.6)));
}
