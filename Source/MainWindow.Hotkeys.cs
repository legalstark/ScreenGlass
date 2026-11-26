using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScreenGlass;

public partial class MainWindow
{
    private HotkeyGesture powerShortcut = HotkeyGesture.PowerDefault;
    private HotkeyGesture automaticShortcut = HotkeyGesture.AutomaticDefault;
    private GlobalHotkey? powerHotkey, automaticHotkey;

    private void InitializeHotkeys()
    {
        powerHotkey = new GlobalHotkey(handle, 0x5347);
        automaticHotkey = new GlobalHotkey(handle, 0x5348);
        ResumeHotkeys();
        Deactivated += (_, _) =>
        {
            if (PowerShortcutBox.IsKeyboardFocusWithin || AutomaticShortcutBox.IsKeyboardFocusWithin)
            {
                Keyboard.ClearFocus();
                ResumeHotkeys();
                ShortcutFeedback.Text = "已结束录制。";
            }
        };
    }

    private void ResumeHotkeys()
    {
        if (exiting) return;
        powerHotkey?.TrySet(powerShortcut);
        if (automaticShortcut != powerShortcut) automaticHotkey?.TrySet(automaticShortcut);
        UpdateHotkeyUi();
    }

    private void UpdateHotkeyUi()
    {
        PowerShortcutBox.Text = powerShortcut.ToString();
        AutomaticShortcutBox.Text = automaticShortcut.ToString();
        PowerShortcutState.Text = powerHotkey is { Registered: true } ? "已生效" : "未生效：组合键可能已被占用，请重新设置。";
        AutomaticShortcutState.Text = automaticHotkey is { Registered: true } ? "已生效" : "未生效：组合键可能已被占用，请重新设置。";
        PowerToggle.ToolTip = $"开启 / 暂停柔光 · {powerShortcut}";
        AutoToggle.ToolTip = automaticShortcut.ToString();
    }

    internal bool SetShortcut(bool automaticAction, HotkeyGesture gesture)
    {
        if (!gesture.IsValid)
        {
            ShortcutFeedback.Text = "请使用 Ctrl、Alt 或 Win 搭配字母、数字或功能键；F12 为系统保留键。";
            return false;
        }
        if (gesture == (automaticAction ? powerShortcut : automaticShortcut))
        {
            ShortcutFeedback.Text = "两个操作不能使用同一组快捷键。";
            return false;
        }
        var binding = automaticAction ? automaticHotkey : powerHotkey;
        if (binding == null || !binding.TrySet(gesture))
        {
            ShortcutFeedback.Text = $"{gesture} 已被占用或无法注册，请换一组组合键。";
            return false;
        }
        if (automaticAction) automaticShortcut = gesture; else powerShortcut = gesture;
        UpdateHotkeyUi();
        UpdateModeLabels();
        Save();
        ShortcutFeedback.Text = "快捷键已保存并立即生效。";
        return true;
    }

    private void ShortcutFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        powerHotkey?.Suspend();
        automaticHotkey?.Suspend();
        ((TextBox)sender).Text = "请按组合键…";
        ShortcutFeedback.Text = "正在录制快捷键，按 Esc 取消；Tab 切换输入框。";
    }

    private void ShortcutBlur(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus == PowerShortcutBox || e.NewFocus == AutomaticShortcutBox)
        {
            ((TextBox)sender).Text = ((TextBox)sender).Tag as string == "automatic" ? automaticShortcut.ToString() : powerShortcut.ToString();
            return;
        }
        ResumeHotkeys();
        if (ShortcutFeedback.Text.StartsWith("正在录制", StringComparison.Ordinal)) ShortcutFeedback.Text = "已结束录制。";
    }

    private void ShortcutKey(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None) return;
        e.Handled = true;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        if (key == Key.Escape)
        {
            Keyboard.ClearFocus();
            ResumeHotkeys();
            ShortcutFeedback.Text = "已取消修改。";
            return;
        }
        if (e.IsRepeat) return;
        if (SetShortcut(((TextBox)sender).Tag as string == "automatic", HotkeyGesture.FromKey(key, Keyboard.Modifiers)))
        {
            Keyboard.ClearFocus();
            ResumeHotkeys();
        }
    }

    private void ResetShortcut(object sender, RoutedEventArgs e)
    {
        bool auto = ((Button)sender).Tag as string == "automatic";
        SetShortcut(auto, auto ? HotkeyGesture.AutomaticDefault : HotkeyGesture.PowerDefault);
    }
}
