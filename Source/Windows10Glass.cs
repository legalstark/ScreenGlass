using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace ScreenGlass;

// Windows 10's native desktop Acrylic. Kept separate from the Windows 11 DWM attributes:
// neither implementation falls back to the other or silently drops the material on failure.
internal sealed class Windows10Glass
{
    private const int WcaAccentPolicy = 19;
    private readonly Window window;
    private readonly IntPtr hwnd;
    private readonly HwndSource source;
    private readonly Brush originalBackground;
    private (int Width, int Height, uint Dpi) lastGeometry;

    private Windows10Glass(Window window)
    {
        this.window = window;
        hwnd = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(hwnd)!;
        originalBackground = window.Background;
    }

    internal static void Attach(Window window)
    {
        var glass = new Windows10Glass(window);
        glass.ApplyMaterial();
        glass.UpdateCorners();
        glass.source.AddHook(glass.WndProc);
        window.Closed += (_, _) => glass.source.RemoveHook(glass.WndProc);
    }

    private void ApplyMaterial()
    {
        // Honor accessibility and the user's Windows transparency preference, as native
        // Windows 11 Acrylic does. This is a system preference, not a compatibility fallback.
        bool transparent = !SystemParameters.HighContrast &&
            Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "EnableTransparency", 1) is int enabled && enabled != 0;
        var accent = new AccentPolicy
        {
            State = transparent ? 4 : 0, // ACCENT_ENABLE_ACRYLICBLURBEHIND / ACCENT_DISABLED
            Flags = 2,
            GradientColor = 0xB3FFFFFF // ABGR, neutral light tint; skin tint stays in WPF.
        };
        int size = Marshal.SizeOf<AccentPolicy>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, buffer, false);
            var data = new CompositionAttributeData { Attribute = WcaAccentPolicy, Data = buffer, Size = (nuint)size };
            if (!SetWindowCompositionAttribute(hwnd, ref data))
                throw new InvalidOperationException("Windows 10 原生磨砂材质初始化失败。请检查显卡驱动和桌面显示环境。");
        }
        finally { Marshal.FreeHGlobal(buffer); }
        window.Background = transparent ? originalBackground : SystemColors.WindowBrush;
    }

    private IntPtr WndProc(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Window size and DPI changes also cover SizeToContent flyouts and cross-monitor moves.
        if (message == 0x0047) UpdateCorners(); // WM_WINDOWPOSCHANGED
        if (message is 0x001A or 0x031A) ApplyMaterial(); // WM_SETTINGCHANGE / WM_THEMECHANGED
        return IntPtr.Zero;
    }

    private void UpdateCorners()
    {
        if (!GetWindowRect(hwnd, out var rect)) throw new Win32Exception(Marshal.GetLastWin32Error());
        uint dpi = GetDpiForWindow(hwnd);
        var geometry = (rect.Right - rect.Left, rect.Bottom - rect.Top, dpi);
        if (geometry.Item1 <= 0 || geometry.Item2 <= 0 || geometry == lastGeometry) return;
        int diameter = (int)Math.Round(16 * dpi / 96d); // Match the existing 8-DIP glass surface.
        IntPtr region = CreateRoundRectRgn(0, 0, geometry.Item1 + 1, geometry.Item2 + 1, diameter, diameter);
        if (region == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        // SetWindowRgn sends WM_WINDOWPOSCHANGED synchronously. Set the guard before calling it.
        var previous = lastGeometry;
        lastGeometry = geometry;
        if (SetWindowRgn(hwnd, region, true) == 0)
        {
            lastGeometry = previous;
            int error = Marshal.GetLastWin32Error();
            DeleteObject(region);
            throw new Win32Exception(error);
        }
        // Success transfers HRGN ownership to Windows. Do not delete the region here.
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy { public int State, Flags; public uint GradientColor; public int AnimationId; }
    [StructLayout(LayoutKind.Sequential)]
    private struct CompositionAttributeData { public int Attribute; public IntPtr Data; public nuint Size; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref CompositionAttributeData data);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);
}
