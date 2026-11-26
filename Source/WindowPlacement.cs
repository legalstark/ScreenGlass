using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace ScreenGlass;

internal static class WindowPlacement
{
    public static void FitMain(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var work = Forms.Screen.FromHandle(hwnd).WorkingArea;
        double scale = GetDpiForWindow(hwnd) / 96d;
        var size = MainSize(new Size(work.Width, work.Height), scale);
        window.Width = size.Width;
        window.Height = size.Height;
        GetWindowRect(hwnd, out var current);
        int width = (int)Math.Round(size.Width * scale), height = (int)Math.Round(size.Height * scale);
        int gap = (int)Math.Ceiling(12 * scale);
        int x = Math.Clamp(current.Left, work.Left + gap, Math.Max(work.Left + gap, work.Right - width - gap));
        int y = Math.Clamp(current.Top, work.Top + gap, Math.Max(work.Top + gap, work.Bottom - height - gap));
        SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, 0x14);
    }

    internal static Size MainSize(Size workPixels, double scale) => new(
        Math.Min(960, Math.Max(1, workPixels.Width / scale - 24)),
        Math.Min(714, Math.Max(1, workPixels.Height / scale - 24)));

    internal static Rect FlyoutBounds(Rect work, Size size, Point anchor, double gap)
    {
        double width = Math.Min(size.Width, Math.Max(1, work.Width - 2 * gap));
        double height = Math.Min(size.Height, Math.Max(1, work.Height - 2 * gap));
        double x = Math.Clamp(anchor.X - width / 2, work.Left + gap, Math.Max(work.Left + gap, work.Right - width - gap));
        double y = Math.Clamp(anchor.Y - height - gap, work.Top + gap, Math.Max(work.Top + gap, work.Bottom - height - gap));
        if (anchor.Y <= work.Top) y = work.Top + gap;
        if (anchor.X <= work.Left) x = work.Left + gap;
        if (anchor.X >= work.Right) x = work.Right - width - gap;
        return new Rect(x, y, width, height);
    }

    public static void ShowFlyout(Window window, System.Drawing.Point anchor)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var area = Forms.Screen.FromPoint(anchor).WorkingArea;
        // Resolve the target monitor's DPI while the window is still hidden.
        SetWindowPos(hwnd, IntPtr.Zero, area.Left + 12, area.Top + 12, 0, 0, 0x15);
        double scale = GetDpiForWindow(hwnd) / 96d;
        window.Width = Math.Min(308, area.Width / scale - 24);
        window.MaxHeight = Math.Max(1, area.Height / scale - 24);
        window.Show();
        window.UpdateLayout();
        GetWindowRect(hwnd, out var actual);
        var bounds = FlyoutBounds(new Rect(area.Left, area.Top, area.Width, area.Height),
            new Size(actual.Right - actual.Left, actual.Bottom - actual.Top),
            new Point(anchor.X, anchor.Y), Math.Ceiling(12 * scale));
        SetWindowPos(hwnd, IntPtr.Zero, (int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height, 0x14);
        window.Activate();
    }

    internal static Rect PhysicalBounds(Window window)
    {
        GetWindowRect(new WindowInteropHelper(window).Handle, out var rect);
        return new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
