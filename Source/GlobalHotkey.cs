using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace ScreenGlass;

public sealed record HotkeyGesture(uint Modifiers, uint VirtualKey)
{
    public static HotkeyGesture PowerDefault => new(3, 0x53);
    public static HotkeyGesture AutomaticDefault => new(3, 0x41);
    [JsonIgnore]
    public bool IsValid => (Modifiers & ~15u) == 0 && (Modifiers & 11) != 0 &&
        (VirtualKey is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A or >= 0x70 and <= 0x87) && VirtualKey != 0x7B;

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & 2) != 0) parts.Add("Ctrl");
        if ((Modifiers & 1) != 0) parts.Add("Alt");
        if ((Modifiers & 4) != 0) parts.Add("Shift");
        if ((Modifiers & 8) != 0) parts.Add("Win");
        parts.Add(VirtualKey >= 0x70 ? $"F{VirtualKey - 0x6F}" : ((char)VirtualKey).ToString());
        return string.Join(" + ", parts);
    }

    internal static HotkeyGesture FromKey(Key key, ModifierKeys modifiers) => new(
        (modifiers.HasFlag(ModifierKeys.Alt) ? 1u : 0) | (modifiers.HasFlag(ModifierKeys.Control) ? 2u : 0) |
        (modifiers.HasFlag(ModifierKeys.Shift) ? 4u : 0) | (modifiers.HasFlag(ModifierKeys.Windows) ? 8u : 0),
        (uint)KeyInterop.VirtualKeyFromKey(key));
}

internal sealed class GlobalHotkey : IDisposable
{
    private readonly IntPtr window;
    private readonly int firstId;
    public int Id { get; private set; }
    public bool Registered { get; private set; }
    public HotkeyGesture? Gesture { get; private set; }

    public GlobalHotkey(IntPtr window, int firstId)
    {
        this.window = window;
        this.firstId = firstId;
        Id = firstId;
    }

    public bool TrySet(HotkeyGesture gesture)
    {
        if (!gesture.IsValid) return false;
        if (Registered && Gesture == gesture) return true;
        int candidateId = Id == firstId ? firstId + 2 : firstId;
        // Reserve the new combination before releasing the current binding.
        if (!RegisterHotKey(window, candidateId, gesture.Modifiers | 0x4000, gesture.VirtualKey)) return false;
        Suspend();
        Id = candidateId;
        Gesture = gesture;
        Registered = true;
        return true;
    }

    public void Suspend()
    {
        if (Registered) UnregisterHotKey(window, Id);
        Registered = false;
    }
    public void Dispose() => Suspend();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
