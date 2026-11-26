using System;

namespace ScreenGlass;

internal static class WindowsSupport
{
    internal const string Requirement = "需要 Windows 10 21H2 / 22H2，或 Windows 11 22H2 及更新版本（x64）。";

    internal static bool IsSupported(Version version) => version.Major == 10 && version.Minor == 0
        && (version.Build is 19044 or 19045 || version.Build >= 22621);

    internal static bool UsesWindows10Glass => Environment.OSVersion.Version.Build < 22000;
}
