using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ScreenGlass.Controls;

public partial class JewelSunglasses : UserControl
{
    public static readonly DependencyProperty StrengthProperty = DependencyProperty.Register(
        nameof(Strength), typeof(double), typeof(JewelSunglasses), new PropertyMetadata(28d, OnAppearanceChanged));
    public static readonly DependencyProperty IsLightEnabledProperty = DependencyProperty.Register(
        nameof(IsLightEnabled), typeof(bool), typeof(JewelSunglasses), new PropertyMetadata(false, OnAppearanceChanged));

    public static readonly DependencyProperty SkinIdProperty = DependencyProperty.Register(
        nameof(SkinId), typeof(string), typeof(JewelSunglasses), new PropertyMetadata("jade", OnAppearanceChanged));
    public string SkinId { get => (string)GetValue(SkinIdProperty); set => SetValue(SkinIdProperty, value); }

    public double Strength { get => (double)GetValue(StrengthProperty); set => SetValue(StrengthProperty, value); }
    public bool IsLightEnabled { get => (bool)GetValue(IsLightEnabledProperty); set => SetValue(IsLightEnabledProperty, value); }
    private bool hovering;

    public JewelSunglasses()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateAppearance(false);
        // Hidden panels retain their chosen appearance without running animation clocks.
        IsVisibleChanged += (_, _) => UpdateAppearance(false);
    }

    private static void OnAppearanceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var jewel = (JewelSunglasses)sender;
        if (jewel.IsInitialized) jewel.UpdateAppearance(jewel.IsVisible);
    }

    private void UpdateAppearance(bool animate)
    {
        double t = Math.Clamp((Strength - 5) / 55, 0, 1);
        animate &= SystemParameters.ClientAreaAnimation;
        var skin = SkinCatalog.Get(SkinId);
        Tint(LensTop, Mix(skin.LightLens[0], skin.DeepLens[0], t), animate);
        Tint(LensMiddle, Mix(skin.LightLens[1], skin.DeepLens[1], t), animate);
        Tint(LensBottom, Mix(skin.LightLens[2], skin.DeepLens[2], t), animate);
        var halo = SkinCatalog.Parse(skin.Accent); halo.A = 150;
        Tint(HaloTint, halo, animate);
        Move(Halo, OpacityProperty, (IsLightEnabled ? 0.32 : 0.14) + t * 0.25, animate);
        Move(HaloScale, ScaleTransform.ScaleXProperty, 0.85 + t * 0.22, animate);
        Move(HaloScale, ScaleTransform.ScaleYProperty, 0.85 + t * 0.22, animate);
        Move(JewelTilt, RotateTransform.AngleProperty, -7 + t * 9 + (hovering ? -2 : 0), animate);
        Move(JewelScale, ScaleTransform.ScaleXProperty, 0.94 + t * 0.06 + (hovering ? 0.04 : 0), animate);
        Move(JewelScale, ScaleTransform.ScaleYProperty, 0.94 + t * 0.06 + (hovering ? 0.04 : 0), animate);
        Move(JewelLift, TranslateTransform.YProperty, hovering ? -3 : 0, animate);
        Move(ReflectionShift, TranslateTransform.XProperty, 40 + t * 170 + (hovering ? 28 : 0), animate);
        Move(Reflection, OpacityProperty, 0.32 + t * 0.3 + (hovering ? 0.15 : 0), animate);
        Move(Sparkle, OpacityProperty, 0.35 + t * 0.4 + (hovering ? 0.2 : 0), animate);
    }

    private static Color Mix(string start, string end, double t)
    {
        var a = (Color)ColorConverter.ConvertFromString(start);
        var b = (Color)ColorConverter.ConvertFromString(end);
        return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    }

    private static void Move(DependencyObject target, DependencyProperty property, double value, bool animate)
    {
        DoubleAnimation? transition = animate ? new DoubleAnimation(value, TimeSpan.FromMilliseconds(240))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } } : null;
        if (target is UIElement element) element.BeginAnimation(property, transition, HandoffBehavior.SnapshotAndReplace);
        else ((Animatable)target).BeginAnimation(property, transition, HandoffBehavior.SnapshotAndReplace);
        if (!animate) target.SetValue(property, value);
    }

    private static void Tint(GradientStop stop, Color color, bool animate)
    {
        if (animate) stop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(color, TimeSpan.FromMilliseconds(220)), HandoffBehavior.SnapshotAndReplace);
        else { stop.BeginAnimation(GradientStop.ColorProperty, null); stop.Color = color; }
    }
    private void OnMouseEnter(object sender, MouseEventArgs e) { hovering = true; UpdateAppearance(true); }
    private void OnMouseLeave(object sender, MouseEventArgs e) { hovering = false; UpdateAppearance(true); }
}
