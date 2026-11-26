using System;
using System.Collections.Generic;
using System.IO;

namespace ScreenGlass;

internal static class AdaptiveVerification
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(string name, bool value)
        {
            if (!value) throw new InvalidOperationException(name);
            checks.Add("PASS " + name);
        }
        bool Near(double a, double b) => Math.Abs(a - b) < 0.000001;
        double Original(double brightness)
        {
            double t = Math.Clamp((brightness - 0.18) / 0.57, 0, 1);
            return 0.38 * t * t * (3 - 2 * t);
        }
        var gate = new AdaptiveLightGate();
        gate.Observe(0.12);
        Check("Dark startup leaves soft light off", Near(gate.Target(38), 0));
        gate.Observe(0.85);
        Check("Dark-to-white responds on the first sample", Near(gate.Target(38), 0.38));
        gate.Observe(0.12);
        Check("White-to-dark responds on the first sample", Near(gate.Target(38), 0));
        gate.Observe(0.35);
        double darkPage = gate.Target(38);
        gate.Observe(0.58);
        Check("Mixed dark/white pages within the former neutral band still respond", Near(darkPage, Original(0.35)) && Near(gate.Target(38), Original(0.58)) && gate.Target(38) > darkPage + 0.15);
        gate.Observe(0.35);
        Check("The reverse mixed-page switch also responds immediately", Near(gate.Target(38), darkPage));
        bool responsive = true;
        for (int i = 0; i < 20; i++)
        {
            gate.Observe(0.58); responsive &= Near(gate.Target(38), Original(0.58));
            gate.Observe(0.35); responsive &= Near(gate.Target(38), darkPage);
        }
        Check("Rapid repeated page switches have no confirmation delay", responsive);
        gate.Observe(0.85);
        bool stable = true;
        for (int i = 0; i < 100; i++)
            foreach (var sample in new[] { 0.95, 0.78, 0.70, 0.66, 0.63, 0.68, 0.85 })
            {
                gate.Observe(sample);
                stable &= Near(gate.Target(38), 0.38);
            }
        Check("700 small white-document fluctuations keep the same shade", stable);
        gate.Reset(); gate.Observe(0.55);
        double whitePage = gate.Target(38);
        foreach (var sample in new[] { 0.535, 0.56, 0.545, 0.565, 0.54 }) gate.Observe(sample);
        Check("White pages below full-screen brightness also suppress small fluctuations", Near(gate.Target(38), whitePage));
        gate.Reset(); gate.Observe(0.50);
        double initial = gate.Target(38);
        foreach (var sample in new[] { 0.51, 0.52, 0.53, 0.54, 0.55 }) gate.Observe(sample);
        Check("Gradual substantial changes are compared to the held shade", gate.Target(38) > initial && Near(gate.Target(38), Original(0.55)));
        gate.Reset(); gate.Observe(0.20); gate.Observe(0.12);
        Check("Fully dark content releases even a small remaining shade", Near(gate.Target(38), 0));
        gate.Reset(); gate.Observe(0.58);
        Check("Reset initializes with the original continuous response", Near(gate.Target(38), Original(0.58)));
        Check("User strength changes apply immediately", Near(gate.Target(50), Original(0.58) * 50 / 38));
        gate.Observe(double.NaN); gate.Observe(double.PositiveInfinity);
        Check("Invalid measurements preserve the current shade", Near(gate.Target(38), Original(0.58)));
        var second = new AdaptiveLightGate(); second.Observe(0.10);
        Check("Displays maintain independent brightness state", Near(second.Target(38), 0) && gate.Target(38) > 0);
        using var overlay = new GlassOverlay(new System.Drawing.Rectangle(0, 0, 100, 100));
        double target = gate.Target(38), previous = 0;
        bool smooth = true;
        for (int i = 0; i < 30; i++)
        {
            overlay.SetShade(target, false);
            smooth &= overlay.Opacity >= previous && overlay.Opacity <= target;
            previous = overlay.Opacity;
        }
        Check("Updated target still fades smoothly and settles", smooth && overlay.Settled && Near(overlay.Opacity, target));
        File.WriteAllLines(Path.Combine(output, "verification.txt"), checks);
    }
}
