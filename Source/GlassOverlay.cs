using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ScreenGlass;

public static class AdaptiveMath
{
    public static double Target(double strength) => Math.Clamp(strength / 100, 0, 0.6);
}

// Stateful gate: two consecutive samples plus a hysteresis band prevent flicker.
public sealed class LowSensitivityGate
{
    public bool Active { get; private set; }
    private int confirmations;
    public void Observe(double brightness, double threshold)
    {
        threshold = Math.Clamp(threshold, 0.3, 0.9);
        var shouldChange = Active ? brightness < threshold - 0.04 : brightness >= threshold;
        confirmations = shouldChange ? confirmations + 1 : 0;
        if (confirmations >= 2) { Active = !Active; confirmations = 0; }
    }
    public double Target(double strength) => Active ? AdaptiveMath.Target(strength) : 0;
    public void Reset() { Active = false; confirmations = 0; }
}

public sealed class GlassOverlay : System.Windows.Forms.Form
{
    public string DisplayId { get; }
    public AdaptiveLightGate AdaptiveLight { get; } = new();
    public LowSensitivityGate LowSensitivity { get; } = new();
    public bool CaptureExcluded { get; private set; }
    private readonly Rectangle physicalBounds;
    public bool Settled { get; private set; }
    protected override bool ShowWithoutActivation => true;
    protected override System.Windows.Forms.CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x20 | 0x80000 | 0x08000000 | 0x80; return p; }
    }
    public GlassOverlay(Rectangle bounds, string displayId = "")
    {
        DisplayId = displayId;
        physicalBounds = bounds;
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        StartPosition = System.Windows.Forms.FormStartPosition.Manual;
        Bounds = bounds;
        Text = "ScreenGlass Soft Light";
        BackColor = System.Drawing.Color.Black;
        Opacity = 0;
        ShowInTaskbar = false;
        TopMost = true;
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        CaptureExcluded = SetWindowDisplayAffinity(Handle, 0x11);
        SetWindowPos(Handle, new IntPtr(-1), physicalBounds.X, physicalBounds.Y, physicalBounds.Width, physicalBounds.Height, 0x10);
    }
    public void SetShade(double target, bool warm)
    {
        BackColor = warm ? System.Drawing.Color.FromArgb(28, 16, 7) : System.Drawing.Color.Black;
        Settled = Math.Abs(Opacity - target) < 0.004;
        var next = Settled ? target : Opacity + (target - Opacity) * 0.35;
        if (Math.Abs(Opacity - next) > 0.0001) Opacity = next;
    }
    public void ResetShade() { Opacity = 0; Settled = true; }
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
}

public static class ScreenSampler
{
    private static readonly Bitmap sample = new(48, 27);
    private static readonly Graphics graphics = Graphics.FromImage(sample);
    public static double Measure(Rectangle bounds)
    {
        // Only a 48 × 27 in-memory luminance sample; no screenshots are written.
        var source = GetDC(IntPtr.Zero);
        if (source == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
        var destination = graphics.GetHdc();
        try
        {
            if (!StretchBlt(destination, 0, 0, 48, 27, source, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x00CC0020))
                throw new System.ComponentModel.Win32Exception();
        }
        finally { graphics.ReleaseHdc(destination); ReleaseDC(IntPtr.Zero, source); }
        double total = 0;
        for (int y = 0; y < 27; y++)
            for (int x = 0; x < 48; x++)
            {
                var pixel = sample.GetPixel(x, y);
                total += (0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B) / 255;
            }
        return total / (48 * 27);
    }
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool StretchBlt(IntPtr dest, int x, int y, int width, int height, IntPtr source, int sx, int sy, int sw, int sh, uint rop);
}

