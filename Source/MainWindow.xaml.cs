using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ScreenGlass;

public partial class MainWindow : Window
{
    private readonly List<GlassOverlay> overlays = new();
    internal IReadOnlyList<DisplayInfo> Displays { get; private set; } = Array.Empty<DisplayInfo>();
    private Dictionary<string, DisplayProfile> displayProfiles = new();
    internal DisplayProfile Profile(string id)
    {
        if (!displayProfiles.TryGetValue(id, out var profile)) displayProfiles[id] = profile = new DisplayProfile();
        return profile;
    }
    internal void ChangeDisplay(string id, bool active, double dimming, bool warm, bool softLight = true)
    {
        var profile = Profile(id);
        bool resetScene = profile.Enabled != active || profile.SoftLight != softLight;
        profile.Enabled = active;
        profile.Dimming = Math.Clamp(dimming, 0, 90);
        profile.Warm = warm;
        profile.SoftLight = softLight;
        foreach (var overlay in overlays)
            if (overlay.DisplayId == id)
            {
                if (resetScene) { overlay.AdaptiveLight.Reset(); overlay.LowSensitivity.Reset(); }
                if (!active) overlay.ResetShade();
            }
        WakeRendering();
        Save();
        ControlsChanged?.Invoke();
    }
    private void WakeRendering() { if (enabled) { ticks = 5; timer.Start(); } }

    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(160) };
    private readonly Forms.NotifyIcon tray;
    private readonly DispatcherTimer trayClickTimer = new();
    private QuickPanel? quickPanel;
    private System.Drawing.Point trayAnchor;
    internal event Action? ControlsChanged;
    internal bool IsPowerOn => enabled;
    internal bool IsAutomatic => automatic;
    internal bool IsLowSensitivity => lowSensitivity;
    internal double Strength => strength;
    internal string Preset => preset;
    private readonly InteractionSound sounds = new();
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly string settingsPath;
    internal bool IsSoundEnabled => sounds.Enabled;
    internal bool IsPresetActive(string value) => preset == value && Math.Abs(strength - PresetStrength(value)) < 0.5;
    private static double PresetStrength(string value) => value switch { "read" => 38, "night" => 50, _ => 28 };
    private bool ready, enabled, automatic = true, exiting;
    private bool lowSensitivity;
    private double triggerBrightness = 65;
    private double strength = 28;
    private string preset = "daily";
    private string skinId = "jade";
    internal string SkinId => skinId;
    internal string? SoundError => sounds.LastError;
    private int ticks;
    private IntPtr handle;

    public MainWindow() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenGlass", "settings.json")) { }

    internal MainWindow(string preferencesPath)
    {
        settingsPath = preferencesPath;
        InitializeComponent();
        GlassWindow.Attach(this);
        System.Windows.Shell.WindowChrome.GetWindowChrome(this).CaptionHeight = 48;
        sounds.StatusChanged += UpdateSoundFeedback;
        SourceInitialized += (_, _) => WindowPlacement.FitMain(this);
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(() => WindowPlacement.FitMain(this)));
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); WritePreferences(); };
        StrengthSlider.AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent,
            new System.Windows.Controls.Primitives.DragCompletedEventHandler((_, _) => PlayTouch()));
        StrengthSlider.PreviewMouseLeftButtonUp += (_, _) => PlayTouch();
        StrengthSlider.PreviewKeyUp += (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown) PlayTouch(); };
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/ScreenGlass;component/Assets/ScreenGlass.ico"));
        tray = new Forms.NotifyIcon { Text = "ScreenGlass · 柔光已暂停", Icon = CreateTrayIcon(), Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开控制面板", null, (_, _) => Dispatcher.Invoke(ShowPanel));
        menu.Items.Add("开启 / 暂停柔光", null, (_, _) => Dispatcher.Invoke(TogglePower));
        menu.Items.Add("开启 / 关闭智能跟随", null, (_, _) => Dispatcher.Invoke(ToggleAutomatic));
        menu.Items.Add(new Forms.ToolStripSeparator());
        var updateItem = menu.Items.Add("检查更新…");
        updateItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() => CheckForUpdates(updateItem)));
        menu.Items.Add("退出 ScreenGlass", null, (_, _) => Dispatcher.Invoke(Exit));
        tray.ContextMenuStrip = menu;
        trayClickTimer.Interval = TimeSpan.FromMilliseconds(Forms.SystemInformation.DoubleClickTime);
        trayClickTimer.Tick += (_, _) =>
        {
            trayClickTimer.Stop();
            quickPanel ??= new QuickPanel(this);
            if (quickPanel.IsVisible) quickPanel.Hide();
            else quickPanel.ShowAt(trayAnchor);
        };
        tray.MouseClick += (_, e) => Dispatcher.Invoke(() =>
        {
            if (e.Button != Forms.MouseButtons.Left) return;
            trayAnchor = Forms.Cursor.Position;
            trayClickTimer.Stop();
            trayClickTimer.Start();
        });
        tray.MouseDoubleClick += (_, e) => Dispatcher.Invoke(() =>
        {
            if (e.Button == Forms.MouseButtons.Left) ShowPanel();
        });
        tray.MouseDown += (_, e) => Dispatcher.Invoke(() =>
        {
            if (e.Button == Forms.MouseButtons.Right) { trayClickTimer.Stop(); quickPanel?.Hide(); }
        });
        timer.Tick += Tick;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
        Closing += (_, e) => { if (!exiting) { e.Cancel = true; Hide(); } };
    }

    private static System.Drawing.Icon CreateTrayIcon()
    {
        using var resource = Application.GetResourceStream(new Uri("pack://application:,,,/ScreenGlass;component/Assets/ScreenGlass.ico"))!.Stream;
        return new System.Drawing.Icon(resource, 32, 32);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ready) return;
        handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowMessage);
        VersionCaption.Text = $"JEWEL COLLECTION    /    {ReleaseUpdates.CurrentVersion}";
        SettingsStatus.Text = $"ScreenGlass {ReleaseUpdates.CurrentVersion} · JEWEL COLLECTION";
        LoadPreferences();
        InitializeHotkeys();
        BuildOverlays();
        DisplayControls.Attach(this);
    }

    internal void LoadPreferences()
    {
        try
        {
            if (File.Exists(settingsPath))
            {
                var saved = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(settingsPath));
                if (saved != null)
                {
                    displayProfiles = saved.Displays ?? new();
                    foreach (var profile in displayProfiles.Values) profile.Dimming = double.IsFinite(profile.Dimming) ? Math.Clamp(profile.Dimming, 0, 90) : 0;
                    strength = Math.Clamp(saved.Strength, 5, 60);
                    automatic = saved.Automatic;
                    sounds.Enabled = saved.SoundEnabled;
                    skinId = SkinCatalog.Contains(saved.Skin) ? saved.Skin : "jade";
                    lowSensitivity = saved.LowSensitivity;
                    triggerBrightness = Math.Clamp(saved.TriggerBrightness, 30, 90);
                    preset = saved.Preset is "read" or "night" ? saved.Preset : "daily";
                    powerShortcut = saved.PowerShortcut is { IsValid: true } ? saved.PowerShortcut : HotkeyGesture.PowerDefault;
                    automaticShortcut = saved.AutomaticShortcut is { IsValid: true } ? saved.AutomaticShortcut : HotkeyGesture.AutomaticDefault;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { SettingsStatus.Text = "配置未能读取，本次使用默认设置。"; }
        SkinCatalog.Apply(skinId);
        UpdateSkinButtons();
        StrengthSlider.Value = strength;
        AutoToggle.IsChecked = automatic;
        SoundToggle.IsChecked = sounds.Enabled;
        UpdateSoundFeedback();
        LowToggle.IsChecked = lowSensitivity;
        ThresholdSlider.Value = triggerBrightness;
        ready = true;
        StrengthLabel.Text = $"{strength:0}%";
        UpdateModeLabels();
        UpdatePresets();
        UpdateHotkeyUi();
    }

    internal void BuildOverlays(IReadOnlyList<DisplayInfo>? displays = null)
    {
        var detected = displays ?? DisplayInfo.Detect();
        foreach (var overlay in overlays) overlay.Close();
        overlays.Clear();
        Displays = detected;
        foreach (var screen in Displays)
        {
            Profile(screen.Id);
            var overlay = new GlassOverlay(screen.Bounds, screen.Id);
            overlays.Add(overlay);
            overlay.Show();
            if (!overlay.CaptureExcluded) SettingsStatus.Text = "此系统无法排除遮罩采样，已停止该屏智能采样；固定调暗仍可使用。";
        }
        WakeRendering();
        ControlsChanged?.Invoke();
    }

    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(() =>
    {
        if (exiting) return;
        try { BuildOverlays(); WindowPlacement.FitMain(this); quickPanel?.Hide(); }
        catch (InvalidOperationException ex) { SettingsStatus.Text = ex.Message; }
    }));

    private void Tick(object? sender, EventArgs e)
    {
        ticks++;
        foreach (var overlay in overlays)
        {
            var profile = Profile(overlay.DisplayId);
            if (enabled && profile.Enabled && profile.SoftLight && automatic && overlay.CaptureExcluded && ticks % 6 == 0)
            {
                try
                {
                    var brightness = ScreenSampler.Measure(overlay.Bounds);
                    if (lowSensitivity) overlay.LowSensitivity.Observe(brightness, triggerBrightness / 100);
                    else overlay.AdaptiveLight.Observe(brightness);
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or ExternalException or ArgumentException)
                {
                    // Hold the previous sample/gate rather than oscillating on capture failures.
                    AdaptiveDescription.Text = "暂时无法采样，保持上次柔光状态";
                }
            }
            var adaptive = !automatic ? AdaptiveMath.Target(strength) : !overlay.CaptureExcluded ? 0 : lowSensitivity
                ? overlay.LowSensitivity.Target(strength)
                : overlay.AdaptiveLight.Target(strength);
            var target = enabled && profile.Enabled ? DimmingMath.Combine(profile.Dimming, profile.SoftLight ? adaptive : 0) : 0;
            overlay.SetShade(target, profile.Warm);
        }
        if (!enabled || ((!automatic || !overlays.Exists(o => Profile(o.DisplayId).Enabled && Profile(o.DisplayId).SoftLight && o.CaptureExcluded)) && overlays.TrueForAll(o => o.Settled)))
        {
            timer.Stop();
        }
    }

    internal void TogglePower()
    {
        enabled = !enabled;
        sounds.Play(enabled ? "on" : "off");
        PowerToggle.IsChecked = enabled;
        StatusTitle.Text = enabled ? "柔光，正在轻轻守护" : "柔光，已暂停";
        PowerLabel.Text = enabled ? "正在运行" : "点击开启";
        tray.Text = enabled ? "ScreenGlass · 柔光运行中" : "ScreenGlass · 柔光已暂停";
        if (enabled) WakeRendering(); else timer.Stop();
        foreach (var overlay in overlays) { overlay.AdaptiveLight.Reset(); overlay.LowSensitivity.Reset(); if (!enabled) overlay.ResetShade(); }
        UpdateModeLabels();
    }

    private void PowerClick(object sender, RoutedEventArgs e) => TogglePower();
    private void AutoClick(object sender, RoutedEventArgs e)
    {
        ToggleAutomatic();
    }
    internal void ToggleAutomatic()
    {
        automatic = !automatic;
        sounds.Play(automatic ? "on" : "off");
        AutoToggle.IsChecked = automatic;
        ResetGates();
        UpdateModeLabels();
        Save();
    }
    private void ResetGates()
    {
        foreach (var overlay in overlays) { overlay.AdaptiveLight.Reset(); overlay.LowSensitivity.Reset(); }
        WakeRendering();
    }
    private void UpdateModeLabels()
    {
        AdaptiveDescription.Text = !automatic ? $"保持固定强度 · {automaticShortcut} 切换" : lowSensitivity
            ? $"低敏模式 · 触发亮度 {triggerBrightness:0}% · 使用固定柔光强度"
            : $"明暗及时跟随 · 小幅波动保持稳定 · {automaticShortcut} 切换";
        ThresholdLabel.Text = $"{triggerBrightness:0}%";
        LowDescription.Text = lowSensitivity ? "连续明亮才开启柔光，阈值附近保持稳定" : "减少页面变化引起的反复调节";
        LowToggle.IsEnabled = automatic;
        LowToggle.Opacity = automatic ? 1 : 0.4;
        ControlsChanged?.Invoke();
        if (!enabled) StatusDescription.Text = "开启后，为明亮页面轻轻降一档。";
        if (enabled) StatusDescription.Text = !automatic ? "保持你喜欢的亮度，安心专注眼前。" : lowSensitivity
            ? "亮到触发线再柔光，轻微变化不打扰。" : "及时跟随页面明暗，过滤阅读时的小幅波动。";
    }
    private void LowClick(object sender, RoutedEventArgs e)
    {
        SetLowSensitivity(LowToggle.IsChecked == true);
    }
    internal void SetLowSensitivity(bool value)
    {
        lowSensitivity = value;
        sounds.Play(value ? "on" : "off");
        LowToggle.IsChecked = value;
        ResetGates();
        UpdateModeLabels();
        Save();
    }
    private void ThresholdChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        triggerBrightness = e.NewValue;
        ResetGates();
        UpdateModeLabels();
        Save();
    }
    private void StrengthChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        strength = e.NewValue;
        WakeRendering();
        StrengthLabel.Text = $"{strength:0}%";
        UpdatePresets();
        Save();
    }
    private void PresetClick(object sender, RoutedEventArgs e)
    {
        SetPreset((string)((Button)sender).Tag);
    }
    internal void SetStrength(double value) => StrengthSlider.Value = Math.Clamp(value, 5, 60);
    internal void SetPreset(string value)
    {
        preset = value;
        StrengthSlider.Value = PresetStrength(preset);
        PlayTouch();
        UpdatePresets();
        Save();
    }
    private void UpdatePresets()
    {
        foreach (var button in new[] { DailyButton, ReadButton, NightButton })
        {
            bool selected = IsPresetActive((string)button.Tag);
            button.SetResourceReference(Control.BackgroundProperty, selected ? "SelectedSurface" : "GlassCard");
            button.SetResourceReference(Control.BorderBrushProperty, selected ? "SelectedStroke" : "GlassCard");
        }
        ControlsChanged?.Invoke();
    }
    internal void PlayTouch() => sounds.Play("touch");
    internal void ToggleSound()
    {
        sounds.Enabled = !sounds.Enabled;
        SoundToggle.IsChecked = sounds.Enabled;
        if (sounds.Enabled) PlayTouch();
        ControlsChanged?.Invoke();
        Save();
    }
    private void SoundClick(object sender, RoutedEventArgs e) => ToggleSound();
    private void LensWheel(object sender, MouseWheelEventArgs e)
    {
        SetStrength(strength + Math.Sign(e.Delta) * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 5 : 1));
        PlayTouch();
        e.Handled = true;
    }
    internal void SetSkin(string id)
    {
        if (id == skinId) return;
        SkinCatalog.Get(id);
        skinId = id;
        SkinCatalog.Apply(id);
        UpdateSkinButtons();
        UpdatePresets();
        PlayTouch();
        Save();
    }
    private void UpdateSkinButtons()
    {
        foreach (var button in new[] { JadeSkin, SapphireSkin, RoseSkin, AmberSkin })
        {
            bool selected = (string)button.Tag == skinId;
            button.SetResourceReference(Control.BackgroundProperty, selected ? "SelectedSurface" : "GlassCard");
            button.SetResourceReference(Control.BorderBrushProperty, selected ? "SelectedStroke" : "GlassCard");
        }
    }
    private void SkinClick(object sender, RoutedEventArgs e) => SetSkin((string)((Button)sender).Tag);
    internal void PreviewSound() => sounds.Preview();
    private void PreviewSoundClick(object sender, RoutedEventArgs e) => PreviewSound();
    private void UpdateSoundFeedback()
    {
        PreviewSoundButton.IsEnabled = !sounds.IsPlaying;
        SoundFeedback.Text = sounds.LastError ?? (sounds.IsPlaying ? "正在试听…" : "可试听确认音量；听不到时请检查输出设备与应用音量。");
        ControlsChanged?.Invoke();
    }
    private void Save()
    {
        if (!ready) return;
        saveTimer.Stop(); saveTimer.Start();
    }
    internal void WritePreferences()
    {
        if (!ready) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            string pending = settingsPath + ".tmp";
            File.WriteAllText(pending, JsonSerializer.Serialize(new Preferences(strength, automatic, preset, lowSensitivity, triggerBrightness, sounds.Enabled, skinId, displayProfiles, powerShortcut, automaticShortcut)));
            File.Move(pending, settingsPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SettingsStatus.Text = "当前无法保存偏好，本次调整仍然有效。"; }
    }
    private void HomeClick(object sender, RoutedEventArgs e) { HomePage.Visibility = Visibility.Visible; SettingsPage.Visibility = Visibility.Collapsed; }
    private void SettingsClick(object sender, RoutedEventArgs e) { HomePage.Visibility = Visibility.Collapsed; SettingsPage.Visibility = Visibility.Visible; }
    private void QuickClick(object sender, RoutedEventArgs e)
    {
        quickPanel ??= new QuickPanel(this);
        var point = PointToScreen(new Point(ActualWidth - 190, 55));
        quickPanel.ShowAt(new System.Drawing.Point((int)point.X, (int)point.Y + 400));
    }
    private void CloseClick(object sender, RoutedEventArgs e) => Hide();
    private void MinimizeClick(object sender, RoutedEventArgs e) => Hide();
    internal void ShowPanel() { trayClickTimer.Stop(); quickPanel?.Hide(); Show(); WindowState = WindowState.Normal; WindowPlacement.FitMain(this); Activate(); }
    private void ExitClick(object sender, RoutedEventArgs e) => Exit();
    private void Exit()
    {
        exiting = true;
        saveTimer.Stop();
        WritePreferences();
        sounds.Dispose();
        timer.Stop();
        trayClickTimer.Stop();
        quickPanel?.Close();
        DisposeDesktopResources();
        Application.Current.Shutdown();
    }
    internal void DisposeForVerification()
    {
        exiting = true;
        saveTimer.Stop(); timer.Stop(); trayClickTimer.Stop(); sounds.Dispose();
        DisposeDesktopResources();
        Close();
    }
    private void DisposeDesktopResources()
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        powerHotkey?.Dispose();
        automaticHotkey?.Dispose();
        foreach (var overlay in overlays) overlay.Close();
        tray.Visible = false;
        tray.Icon?.Dispose();
        tray.Dispose();
    }
    private IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && powerHotkey is { Registered: true } && wParam.ToInt32() == powerHotkey.Id) { TogglePower(); handled = true; }
        if (msg == 0x0312 && automaticHotkey is { Registered: true } && wParam.ToInt32() == automaticHotkey.Id) { ToggleAutomatic(); handled = true; }
        if (msg == 0x8001) { ShowPanel(); handled = true; }
        return IntPtr.Zero;
    }
    public record Preferences(double Strength, bool Automatic, string Preset, bool LowSensitivity = false, double TriggerBrightness = 65, bool SoundEnabled = true, string Skin = "jade", Dictionary<string, DisplayProfile>? Displays = null, HotkeyGesture? PowerShortcut = null, HotkeyGesture? AutomaticShortcut = null);
}
