using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace ScreenGlass;

internal static class PlatformVerification
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string> { $"Host: {Environment.OSVersion.Version}; x64: {Environment.Is64BitProcess}",
            $"Selected material: {(WindowsSupport.UsesWindows10Glass ? "Windows 10 Acrylic" : "Windows 11 DWM Acrylic")}",
            "Windows 10-specific visual quality and interactive performance require a real Windows 10 machine." };
        void Check(string name, bool value)
        {
            if (!value) throw new InvalidOperationException(name);
            checks.Add("PASS " + name);
            File.WriteAllLines(Path.Combine(output, "platform-verification.txt"), checks);
        }

        foreach (int build in new[] { 19044, 19045, 22621, 22631, 26100, 26200 })
            Check($"Supported build {build}", WindowsSupport.IsSupported(new Version(10, 0, build)));
        foreach (var version in new[] { new Version(6, 3, 9600), new Version(10, 0, 17763),
            new Version(10, 0, 19041), new Version(10, 0, 19042), new Version(10, 0, 19043), new Version(10, 0, 22000) })
            Check($"Excluded version {version}", !WindowsSupport.IsSupported(version));

        // Exercise Windows 10 interop even on the Windows 11 build machine. API success
        // here does not substitute for testing Acrylic appearance/performance on Windows 10.
        var probe = new Window
        {
            Title = "ScreenGlass material verification", Width = 308, Height = 380,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            Background = Brushes.Transparent, ShowInTaskbar = false
        };
        WindowChrome.SetWindowChrome(probe, new WindowChrome
        {
            CaptionHeight = 0, ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(-1), UseAeroCaptionButtons = false
        });
        probe.SourceInitialized += (_, _) =>
        {
            HwndSource.FromHwnd(new WindowInteropHelper(probe).Handle)!.CompositionTarget.BackgroundColor = Colors.Transparent;
            Windows10Glass.Attach(probe);
        };
        try
        {
            probe.Show(); Flush(probe);
            var hwnd = new WindowInteropHelper(probe).Handle;
            Check("Windows 10 Acrylic interop initializes", probe.IsLoaded);
            AssertCorners(hwnd, Check);
            probe.Width = 440; probe.Height = 510; Flush(probe);
            AssertCorners(hwnd, Check);
            Check("Windows 10 material survives hide/show", HideAndShow(probe));
            SendMessage(hwnd, 0x001A, IntPtr.Zero, IntPtr.Zero);
            SendMessage(hwnd, 0x031A, IntPtr.Zero, IntPtr.Zero);
            Check("Windows 10 material reapplies after theme/settings notifications", probe.IsVisible);
        }
        finally { probe.Close(); }

        // Only the actual windows touched by the material change; keep personal settings isolated.
        string settings = Path.Combine(output, "platform-preferences.json");
        File.WriteAllText(settings, JsonSerializer.Serialize(new MainWindow.Preferences(28, true, "daily", false, 65, false)));
        var main = new MainWindow(settings);
        var quick = new QuickPanel(main);
        try
        {
            main.Show(); Flush(main);
            Check("Main window initializes with the host's selected native material", main.IsVisible && main.IsLoaded);
            Check("Startup keeps soft light paused", !main.IsPowerOn);
            var work = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(main).Handle).WorkingArea;
            quick.ShowAt(new System.Drawing.Point(work.Right - 30, work.Bottom)); Flush(quick);
            Check("Flyout initializes with the host's selected native material", quick.IsVisible && quick.IsLoaded);
            if (WindowsSupport.UsesWindows10Glass)
            {
                AssertCorners(new WindowInteropHelper(main).Handle, Check);
                AssertCorners(new WindowInteropHelper(quick).Handle, Check);
            }
            quick.Hide();
            quick.ShowAt(new System.Drawing.Point(work.Right - 30, work.Bottom)); Flush(quick);
            Check("Flyout reopens with native material attached", quick.IsVisible);
        }
        finally { quick.Close(); main.DisposeForVerification(); }
        checks.Add("NOT TESTED: real Win10 21H2 / 22H2 appearance, dragging, mixed-DPI monitors and GPU behavior (unless this log was produced on that target host).");
        File.WriteAllLines(Path.Combine(output, "platform-verification.txt"), checks);
    }

    private static void Flush(Window window) => window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(window.UpdateLayout));
    private static bool HideAndShow(Window window) { window.Hide(); window.Show(); Flush(window); return window.IsVisible; }

    private static void AssertCorners(IntPtr hwnd, Action<string, bool> check)
    {
        IntPtr region = CreateRectRgn(0, 0, 0, 0);
        if (region == IntPtr.Zero) throw new InvalidOperationException("Could not allocate verification region.");
        try
        {
            check("Rounded HWND region exists", GetWindowRgn(hwnd, region) == 3); // COMPLEXREGION
            check("Rounded corners exclude the top-left pixel", !PtInRegion(region, 0, 0));
            GetWindowRect(hwnd, out var rect);
            int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
            check("Region covers current window center", PtInRegion(region, width / 2, height / 2));
            check("Region follows window resizing", PtInRegion(region, width - 2, height / 2) && !PtInRegion(region, width + 2, height / 2));
        }
        finally { DeleteObject(region); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool PtInRegion(IntPtr region, int x, int y);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr hwnd, IntPtr region);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
}
