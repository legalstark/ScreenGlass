using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenGlass;

internal static class ReleaseVerification
{
    public static IReadOnlyList<string> Run(string output, bool windowSmoke = false)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(string name, bool value) { if (!value) throw new InvalidOperationException(name); checks.Add("PASS " + name); }
        // Window sizes use physical work areas and logical content sizes; verify mixed-DPI edge cases.
        foreach (var work in new[] { new Size(1920, 1040), new Size(2560, 1400), new Size(3840, 2120), new Size(1366, 728) })
        foreach (double scale in new[] { 1d, 1.25, 1.5, 1.75, 2d })
        {
            var size = WindowPlacement.MainSize(work, scale);
            Check($"Main fits {work.Width}x{work.Height} at {scale * 100}%", size.Width * scale <= work.Width && size.Height * scale <= work.Height);
            foreach (var origin in new[] { new Point(0, 0), new Point(-2560, -200) })
            {
                var area = new Rect(origin, work);
                foreach (var anchor in new[] { area.BottomRight, area.TopLeft, new Point(area.Left, area.Top + work.Height / 2), new Point(area.Right, area.Top + work.Height / 2) })
                {
                    var flyout = WindowPlacement.FlyoutBounds(area, new Size(308 * scale, 410 * scale), anchor, 12 * scale);
                    Check($"Flyout stays inside work area at {scale * 100}% / {anchor}", area.Contains(flyout));
                }
            }
        }
        var prefs = new MainWindow.Preferences(36, true, "read", true, 70, false);
        Check("Sound preference round-trips", JsonSerializer.Deserialize<MainWindow.Preferences>(JsonSerializer.Serialize(prefs)) == prefs);
        foreach (var cue in new[] { "on", "off", "touch" })
        {
            using var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/ScreenGlass;component/Assets/Sounds/{cue}.wav"))!.Stream;
            using var reader = new BinaryReader(stream);
            Check($"Embedded {cue} cue is WAV", new string(reader.ReadChars(4)) == "RIFF");
            Check($"Embedded {cue} cue is short", stream.Length < 10000);
        }
        var settings = Path.Combine(output, "preferences-test.json");
        File.WriteAllText(settings, JsonSerializer.Serialize(new MainWindow.Preferences(28, true, "daily", false, 65, false)));
        var main = new MainWindow(settings);
        main.LoadPreferences();
        var quick = new QuickPanel(main);
        try
        {
            typeof(QuickPanel).GetMethod("Refresh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(quick, null);
            if (windowSmoke)
            {
                var physicalQuick = new QuickPanel(main);
                var screen = System.Windows.Forms.Screen.PrimaryScreen!;
                physicalQuick.ShowAt(new System.Drawing.Point(screen.WorkingArea.Right - 30, screen.WorkingArea.Bottom + 10));
                physicalQuick.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => { }));
                physicalQuick.UpdateLayout();
                var scroll = (ScrollViewer)physicalQuick.FindName("QuickScroll");
                File.WriteAllText(Path.Combine(output, "window-layout.txt"), $"Window {physicalQuick.ActualWidth}x{physicalQuick.ActualHeight}; scroll {scroll.ActualWidth}x{scroll.ActualHeight}; extent {scroll.ExtentWidth}x{scroll.ExtentHeight}; viewport {scroll.ViewportWidth}x{scroll.ViewportHeight}; computed {scroll.ComputedVerticalScrollBarVisibility}; max {physicalQuick.MaxHeight}; size {physicalQuick.SizeToContent}");
                var native = WindowPlacement.PhysicalBounds(physicalQuick);
                var area = screen.WorkingArea;
                Check("Real flyout stays inside monitor work area", new Rect(area.Left, area.Top, area.Width, area.Height).Contains(native));
                double dpi = VisualTreeHelper.GetDpi(physicalQuick).DpiScaleY;
                Check("Real flyout respects content height at current DPI", Math.Abs(native.Height - physicalQuick.ActualHeight * dpi) <= 2);
                Check("Real flyout is compact", physicalQuick.ActualHeight <= 420);
                if (physicalQuick.MaxHeight > 420)
                    Check("Normal flyout has no unnecessary scrollbar", scroll.ComputedVerticalScrollBarVisibility == Visibility.Collapsed);
                physicalQuick.Close();
                var startup = new MainWindow(settings);
                try
                {
                    startup.Show();
                    startup.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => { }));
                    var startupBounds = WindowPlacement.PhysicalBounds(startup);
                    var startupArea = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(startup).Handle).WorkingArea;
                    Check("Normal startup loads the full working panel", startup.IsVisible && startup.IsLoaded && startup.Strength == 28);
                    Check("Normal startup leaves soft light paused", !startup.IsPowerOn);
                    Check("Real main window stays inside work area", new Rect(startupArea.Left, startupArea.Top, startupArea.Width, startupArea.Height).Contains(startupBounds));
                }
                finally { startup.DisposeForVerification(); }
            }
            Check("Mute setting loads without playing audio", !main.IsSoundEnabled);
            main.SetPreset("read");
            Check("Preset immediately synchronizes strength", main.Strength == 38 && ((Slider)quick.FindName("StrengthControl")).Value == 38);
            main.SetStrength(35);
            Check("Custom strength clears preset selection", !main.IsPresetActive("read"));
            main.ToggleAutomatic();
            Check("Manual mode disables low-sensitivity controls in both panels", !main.IsAutomatic && !((ToggleButton)quick.FindName("LowSwitch")).IsEnabled && !((ToggleButton)main.FindName("LowToggle")).IsEnabled);
            main.TogglePower();
            Check("Power immediately synchronizes to quick panel", main.IsPowerOn && ((ToggleButton)quick.FindName("PowerSwitch")).IsChecked == true);
            main.TogglePower();
            main.WritePreferences();
            var saved = JsonSerializer.Deserialize<MainWindow.Preferences>(File.ReadAllText(settings));
            Check("Preferences persist strength, mode and mute", saved is { Strength: 35, Automatic: false, SoundEnabled: false });
            // Return to the default scene for the visual reference, with audio still muted.
            main.ToggleAutomatic(); main.SetPreset("daily");
            var quickContent = (FrameworkElement)quick.Content;
            quick.Content = null;
            quickContent.Measure(new Size(308, double.PositiveInfinity));
            double height = Math.Ceiling(quickContent.DesiredSize.Height);
            Check("Pocket panel uses intrinsic height", quick.SizeToContent == SizeToContent.Height && height <= 420);
            Check("Pocket panel is substantially smaller", 308 * height < 354 * 550 * 0.7);
            var slider = (Slider)quick.FindName("StrengthControl");
            Check("Quick strength synchronizes before showing", Math.Abs(slider.Value - main.Strength) < 0.01);
            Check("Quick mute button is present", quick.FindName("SoundButton") is Button);
            foreach (double scale in new[] { 1d, 1.25, 1.5, 1.75, 2d })
            {
                Render(quickContent, 308, height, scale, Path.Combine(output, $"quick-{scale * 100:0}.png"));
                Check($"Quick panel rendered at {scale * 100}%", quickContent.RenderSize.Height == height);
            }
            var mainContent = (FrameworkElement)main.Content;
            main.Content = null;
            foreach (var size in new[] { new Size(960, 714), new Size(936, 496), new Size(886, 461) })
                Render(mainContent, size.Width, size.Height, 1, Path.Combine(output, $"main-{size.Width:0}x{size.Height:0}.png"));
            Check("Main page scrolls on high DPI screens", main.FindName("HomePage") is ScrollViewer { VerticalScrollBarVisibility: ScrollBarVisibility.Auto });
            Check("Sound preference is available in settings", main.FindName("SoundToggle") is ToggleButton);
        }
        finally
        {
            quick.Close();
            main.DisposeForVerification();
        }
        File.WriteAllLines(Path.Combine(output, "verification.txt"), checks);
        return checks;
    }

    private static void Render(FrameworkElement content, double width, double height, double scale, string path)
    {
        var surface = new Border { Width = width, Height = height, Background = new SolidColorBrush(Color.FromRgb(225, 237, 230)), Child = content };
        surface.Measure(new Size(width, height)); surface.Arrange(new Rect(0, 0, width, height)); surface.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
        surface.Child = null;
    }
}
