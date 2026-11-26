using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace ScreenGlass;

// Use the active Windows display topology for both the model name and device identity.
internal static class NativeDisplays
{
    private const uint OnlyActivePaths = 2;
    private const int InsufficientBuffer = 122;

    internal static IReadOnlyList<DisplayInfo> Detect()
    {
        var names = new Dictionary<string, List<TargetName>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in ActivePaths())
        {
            var source = new SourceName { Header = Header(1, Marshal.SizeOf<SourceName>(), path.Source.Adapter, path.Source.Id) };
            var target = new TargetName { Header = Header(2, Marshal.SizeOf<TargetName>(), path.Target.Adapter, path.Target.Id) };
            Check(DisplayConfigGetDeviceInfo(ref source));
            Check(DisplayConfigGetDeviceInfo(ref target));
            if (string.IsNullOrWhiteSpace(target.DevicePath))
                throw new InvalidOperationException("Windows 未提供显示器设备身份，请重新连接显示器后重试。");
            if (!names.TryGetValue(source.GdiName, out var targets)) names[source.GdiName] = targets = new();
            targets.Add(target);
        }

        var displays = new List<DisplayInfo>();
        foreach (var screen in Forms.Screen.AllScreens)
        {
            if (!names.TryGetValue(screen.DeviceName, out var targets))
                throw new InvalidOperationException("显示器连接状态正在变化，请稍后重新打开面板。");
            // Mirrored outputs share one desktop region and therefore one overlay.
            string model = string.Join(" / ", targets.Select(ModelName));
            displays.Add(new DisplayInfo(targets[0].DevicePath,
                $"{model} · {screen.DeviceName.Replace(@"\\.\", "")} · {screen.Bounds.Width}×{screen.Bounds.Height}{(screen.Primary ? " · 主屏" : "")}", screen.Bounds));
        }
        return displays;
    }

    private static string ModelName(TargetName target)
    {
        if (!string.IsNullOrWhiteSpace(target.FriendlyName)) return target.FriendlyName.Trim();
        return target.Technology is 0x80000000 or 6 or 11 or 13 ? "内置显示器" : "显示器（未提供型号）";
    }

    private static PathInfo[] ActivePaths()
    {
        // Buffer sizes can change between the two calls during a physical hot-plug.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Check(GetDisplayConfigBufferSizes(OnlyActivePaths, out uint pathCount, out uint modeCount));
            var paths = new PathInfo[pathCount];
            var modes = new ModeInfo[modeCount];
            int error = QueryDisplayConfig(OnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (error == InsufficientBuffer) continue;
            Check(error);
            return paths.Take((int)pathCount).ToArray();
        }
        throw new InvalidOperationException("显示器连接状态正在变化，请稍后重试。");
    }

    private static DeviceHeader Header(uint type, int size, Luid adapter, uint id) =>
        new() { Type = type, Size = (uint)size, Adapter = adapter, Id = id };
    private static void Check(int error)
    {
        if (error != 0) throw new InvalidOperationException($"无法读取 Windows 显示器信息：{new Win32Exception(error).Message}");
    }

    [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] private struct Rational { public uint Numerator, Denominator; }
    [StructLayout(LayoutKind.Sequential)] private struct SourceInfo
    {
        public Luid Adapter;
        public uint Id, ModeIndex, StatusFlags;
    }
    [StructLayout(LayoutKind.Sequential)] private struct TargetInfo
    {
        public Luid Adapter;
        public uint Id, ModeIndex, Technology, Rotation, Scaling;
        public Rational RefreshRate;
        public uint ScanlineOrdering;
        public int Available;
        public uint StatusFlags;
    }
    [StructLayout(LayoutKind.Sequential)] private struct PathInfo
    {
        public SourceInfo Source;
        public TargetInfo Target;
        public uint Flags;
    }
    // DISPLAYCONFIG_MODE_INFO includes a 48-byte union; mode contents are not needed here.
    [StructLayout(LayoutKind.Explicit, Size = 64)] private struct ModeInfo { }
    [StructLayout(LayoutKind.Sequential)] private struct DeviceHeader
    {
        public uint Type, Size;
        public Luid Adapter;
        public uint Id;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct SourceName
    {
        public DeviceHeader Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string GdiName;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct TargetName
    {
        public DeviceHeader Header;
        public uint Flags, Technology;
        public ushort Manufacturer, Product;
        public uint Connector;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string FriendlyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DevicePath;
    }
    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint pathCount,
        [Out] PathInfo[] paths, ref uint modeCount, [Out] ModeInfo[] modes, IntPtr topology);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DisplayConfigGetDeviceInfo(ref SourceName name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DisplayConfigGetDeviceInfo(ref TargetName name);
}
