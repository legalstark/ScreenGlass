using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ScreenGlass;

public partial class QuickPanel : Window
{
    private readonly MainWindow controller;
    private bool refreshing = true;

    public QuickPanel(MainWindow controller)
    {
        InitializeComponent();
        this.controller = controller;
        DisplayControls.Attach(controller);
        GlassWindow.Attach(this);
        controller.ControlsChanged += Refresh;
        StrengthControl.AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent,
            new System.Windows.Controls.Primitives.DragCompletedEventHandler((_, _) => controller.PlayTouch()));
        StrengthControl.PreviewMouseLeftButtonUp += (_, _) => controller.PlayTouch();
        StrengthControl.PreviewKeyUp += (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown) controller.PlayTouch(); };
        Closed += (_, _) => controller.ControlsChanged -= Refresh;
        Deactivated += (_, _) => Hide();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Hide(); e.Handled = true; } };
    }

    public void ShowAt(System.Drawing.Point anchor)
    {
        Refresh();
        WindowPlacement.ShowFlyout(this, anchor);
    }

    private void Refresh()
    {
        refreshing = true;
        PowerSwitch.IsChecked = controller.IsPowerOn;
        PowerStatus.Text = controller.IsPowerOn ? "柔光已开启" : "柔光已暂停";
        AutoSwitch.IsChecked = controller.IsAutomatic;
        LowSwitch.IsChecked = controller.IsLowSensitivity;
        LowSwitch.IsEnabled = controller.IsAutomatic;
        LowSwitch.Opacity = controller.IsAutomatic ? 1 : 0.4;
        SkinCaption.Text = $" / {SkinCatalog.Get(controller.SkinId).Name} · {ReleaseUpdates.CurrentVersion}";
        ModeHint.Text = controller.IsAutomatic ? "柔光上限 · 智能跟随" : "柔光强度 · 手动";
        SoundWaves.Visibility = controller.IsSoundEnabled ? Visibility.Visible : Visibility.Collapsed;
        MuteLine.Visibility = controller.IsSoundEnabled ? Visibility.Collapsed : Visibility.Visible;
        SoundButton.ToolTip = controller.IsSoundEnabled ? "音效已开启 · 点击静音，右键试听" : "音效已关闭 · 点击开启，右键试听";
        System.Windows.Automation.AutomationProperties.SetName(SoundButton, controller.IsSoundEnabled ? "关闭交互音效" : "开启交互音效");
        StrengthControl.Value = controller.Strength;
        StrengthValue.Text = $"{controller.Strength:0}%";
        foreach (var button in new[] { Daily, Read, Night })
        {
            bool selected = controller.IsPresetActive((string)button.Tag);
            button.SetResourceReference(Control.BackgroundProperty, selected ? "SelectedSurface" : "GlassCard");
            button.SetResourceReference(Control.BorderBrushProperty, selected ? "SelectedStroke" : "GlassCard");
        }
        foreach (var button in new[] { JadeSkin, SapphireSkin, RoseSkin, AmberSkin })
        {
            bool selected = (string)button.Tag == controller.SkinId;
            button.SetResourceReference(Control.BackgroundProperty, selected ? "SelectedSurface" : "GlassCard");
            button.SetResourceReference(Control.BorderBrushProperty, selected ? "SelectedStroke" : "GlassCard");
        }
        if (controller.SoundError != null) SoundButton.ToolTip = controller.SoundError + " · 右键重试试听";
        refreshing = false;
    }
    private void PowerClick(object sender, RoutedEventArgs e) => controller.TogglePower();
    private void AutoClick(object sender, RoutedEventArgs e) => controller.ToggleAutomatic();
    private void LowClick(object sender, RoutedEventArgs e) => controller.SetLowSensitivity(LowSwitch.IsChecked == true);
    private void StrengthChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (!refreshing) controller.SetStrength(e.NewValue); }
    private void PresetClick(object sender, RoutedEventArgs e) => controller.SetPreset((string)((Button)sender).Tag);
    private void OpenClick(object sender, RoutedEventArgs e) => controller.ShowPanel();
    private void DismissClick(object sender, RoutedEventArgs e) => Hide();
    private void SoundClick(object sender, RoutedEventArgs e) => controller.ToggleSound();
    private void SkinClick(object sender, RoutedEventArgs e) => controller.SetSkin((string)((Button)sender).Tag);
    private void PreviewSoundClick(object sender, MouseButtonEventArgs e) { controller.PreviewSound(); e.Handled = true; }
    private void LensWheel(object sender, MouseWheelEventArgs e)
    {
        controller.SetStrength(controller.Strength + Math.Sign(e.Delta) * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 5 : 1));
        controller.PlayTouch();
        e.Handled = true;
    }
}
