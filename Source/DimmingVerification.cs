using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace ScreenGlass;
internal static class DimmingVerification
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(string name, bool value) { if (!value) throw new InvalidOperationException(name); checks.Add("PASS " + name); }
        var detected = DisplayInfo.Detect();
        Check("Real displays detected with unique stable interfaces", detected.Count > 0 && new HashSet<string>(System.Linq.Enumerable.Select(detected, d => d.Id)).Count == detected.Count);
        Check("Dark content retains fixed 40% dimming", Math.Abs(DimmingMath.Combine(40, new AdaptiveLightGate().Target(28)) - .4) < .0001);
        Check("Alpha combines without exceeding 90%", Math.Abs(DimmingMath.Combine(40, .5) - .7) < .0001 && DimmingMath.Combine(90, .6) == .9);
        var settings = Path.Combine(output, "test-settings.json");
        File.WriteAllText(settings, JsonSerializer.Serialize(new MainWindow.Preferences(28, true, "daily", SoundEnabled: false)));
        var main = new MainWindow(settings);
        var quick = new QuickPanel(main);
        var overlaysField = typeof(MainWindow).GetField("overlays", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var timerField = typeof(MainWindow).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var tick = typeof(MainWindow).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance)!;
        void Step() => tick.Invoke(main, new object?[] { null, EventArgs.Empty });
        void Pump() => main.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
        try
        {
            main.Show(); Pump();
            var timer = (DispatcherTimer)timerField.GetValue(main)!;
            Check("Startup paused without refresh timer", !main.IsPowerOn && !timer.IsEnabled);
            var physical = detected[0].Bounds;
            var screens = new[] { new DisplayInfo("test-A", "测试屏 A", new System.Drawing.Rectangle(physical.X, physical.Y, 200, 120)), new DisplayInfo("test-B", "测试屏 B", new System.Drawing.Rectangle(physical.X + 210, physical.Y, 200, 120)) };
            main.BuildOverlays(screens); Pump();
            main.ChangeDisplay("test-A", true, 40, false);
            main.ChangeDisplay("test-B", true, 15, true);
            if (main.IsAutomatic) main.ToggleAutomatic();
            main.TogglePower();
            for (int i = 0; i < 40; i++) Step();
            var overlays = (List<GlassOverlay>)overlaysField.GetValue(main)!;
            Check("Independent live native overlay opacity", Math.Abs(overlays[0].Opacity - DimmingMath.Combine(40, .28)) < .005 && Math.Abs(overlays[1].Opacity - DimmingMath.Combine(15, .28)) < .005);
            Check("Neutral black and optional warm colors", overlays[0].BackColor == System.Drawing.Color.Black && overlays[1].BackColor.R == 28);
            Check("Native overlays exclude screen capture", overlays.TrueForAll(o => o.CaptureExcluded));
            Check("Fixed mode stops timer after settling", !timer.IsEnabled);
            main.ChangeDisplay("test-A", false, 40, false); Step();
            Check("Disabling A leaves B dimmed", overlays[0].Opacity == 0 && overlays[1].Opacity > .3);
            main.ChangeDisplay("test-B", true, 15, false, false);
            for (int i = 0; i < 40; i++) Step();
            Check("Pure dimming matches slider without extra adaptive shade", Math.Abs(overlays[1].Opacity - .15) < .005);
            main.ToggleAutomatic();
            for (int i = 0; i < 40; i++) Step();
            Check("Pure dimming does not run adaptive sampling timer", !timer.IsEnabled);
            main.ChangeDisplay("test-B", true, 15, true, false);
            main.TogglePower();
            Check("Global pause clears all overlays and stops timer", !timer.IsEnabled && overlays.TrueForAll(o => o.Opacity == 0));
            main.BuildOverlays(new[] { screens[1], screens[0] });
            Check("Reordered displays retain per-device settings", main.Profile("test-A").Dimming == 40 && !main.Profile("test-A").Enabled && main.Profile("test-B").Dimming == 15);
            main.BuildOverlays(new[] { screens[1] }); main.BuildOverlays(screens);
            Check("Disconnect/reconnect preserves profiles", main.Profile("test-A").Dimming == 40);
            main.WritePreferences();
            var saved = JsonSerializer.Deserialize<MainWindow.Preferences>(File.ReadAllText(settings))!;
            Check("Independent settings persisted", saved.Displays!["test-A"].Dimming == 40 && saved.Displays["test-B"].Warm);
            main.BuildOverlays(); Pump();
            quick.ShowAt(new System.Drawing.Point(physical.Right - 30, physical.Bottom - 40)); Pump();
            foreach (var pair in new[] { ("main.png", (FrameworkElement)main.Content), ("quick.png", (FrameworkElement)quick.Content) })
            {
                pair.Item2.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(pair.Item2.ActualWidth), (int)Math.Ceiling(pair.Item2.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(pair.Item2);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(output, pair.Item1)); encoder.Save(stream);
            }
            File.WriteAllText(Path.Combine(output, "displays.json"), JsonSerializer.Serialize(detected, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllLines(Path.Combine(output, "checks.txt"), checks);
        }
        finally { quick.Close(); main.DisposeForVerification(); }
        var restored = new MainWindow(settings);
        try { restored.LoadPreferences(); Check("Profiles restored on new launch", restored.Profile("test-A").Dimming == 40 && restored.Profile("test-B").Warm && !restored.IsPowerOn); }
        finally { restored.DisposeForVerification(); }
        File.WriteAllLines(Path.Combine(output, "checks.txt"), checks);
    }
}
