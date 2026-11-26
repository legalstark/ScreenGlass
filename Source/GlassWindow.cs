using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace ScreenGlass;

internal static class GlassWindow
{
    public static void Attach(Window window)
    {
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = 0, ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(-1), UseAeroCaptionButtons = false
        });
        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            HwndSource.FromHwnd(hwnd)!.CompositionTarget.BackgroundColor = Colors.Transparent;
            if (WindowsSupport.UsesWindows10Glass)
                Windows10Glass.Attach(window);
            else
            {
                int acrylic = 3, rounded = 2;
                Marshal.ThrowExceptionForHR(DwmSetWindowAttribute(hwnd, 38, ref acrylic, sizeof(int)));
                Marshal.ThrowExceptionForHR(DwmSetWindowAttribute(hwnd, 33, ref rounded, sizeof(int)));
            }
            int light = 0;
            Marshal.ThrowExceptionForHR(DwmSetWindowAttribute(hwnd, 20, ref light, sizeof(int)));
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
