using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ScreenGlass;

internal static class PolishVerification
{
    public static void Run(string output, bool audioPreview)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(string name, bool value) { if (!value) throw new InvalidOperationException(name); checks.Add("PASS " + name); }
        foreach (var cue in new[] { "on", "off", "touch" })
        {
            using var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/ScreenGlass;component/Assets/Sounds/{cue}.wav"))!.Stream;
            using var reader = new BinaryReader(stream);
            Check($"{cue}: PCM WAV header", new string(reader.ReadChars(4)) == "RIFF");
            stream.Position = 24; int rate = reader.ReadInt32();
            stream.Position = 40; int length = reader.ReadInt32();
            double sum = 0, peak = 0;
            for (int i = 0; i < length / 2; i++) { double sample = reader.ReadInt16() / 32768d; sum += sample * sample; peak = Math.Max(peak, Math.Abs(sample)); }
            double rms = Math.Sqrt(sum / (length / 2));
            Check($"{cue}: audible signal without clipping (RMS {20 * Math.Log10(rms):0.0} dB)", rms > 0.04 && peak < 0.4);
            Check($"{cue}: concise cue", length / (rate * 2d) <= 0.18);
        }
        var path = Path.Combine(output, "preferences-test.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new MainWindow.Preferences(28, true, "daily", false, 65, false, "jade")));
        var main = new MainWindow(path); main.LoadPreferences();
        var quick = new QuickPanel(main);
        typeof(QuickPanel).GetMethod("Refresh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(quick, null);
        var quickContent = (FrameworkElement)quick.Content; quick.Content = null;
        var snapshots = new List<BitmapSource>();
        try
        {
            var lens = (Button)quick.FindName("LensButton");
            var slider = (Slider)quick.FindName("StrengthControl");
            Check("Lens button has no focus border", lens.FocusVisualStyle == null);
            Check("Slider has no focus border", slider.FocusVisualStyle == null);
            Check("Lens template has no focus outline trigger", !HasFocusBorderTrigger(lens.Template));
            Check("Slider template has no focus outline trigger", !HasFocusBorderTrigger(slider.Template));
            Check("Sound preview button exists", main.FindName("PreviewSoundButton") is Button);
            var distinct = new HashSet<Color>();
            foreach (var skin in SkinCatalog.All)
            {
                main.SetSkin(skin.Id);
                Check($"{skin.Name}: global theme applied", (string)Application.Current.Resources["ActiveSkinId"] == skin.Id);
                Check($"{skin.Name}: compact caption synchronized", ((TextBlock)quick.FindName("SkinCaption")).Text.Contains(skin.Name));
                double strength = main.Strength; bool automatic = main.IsAutomatic;
                main.WritePreferences();
                var saved = JsonSerializer.Deserialize<MainWindow.Preferences>(File.ReadAllText(path))!;
                Check($"{skin.Name}: choice persisted", saved.Skin == skin.Id && !saved.SoundEnabled);
                Check($"{skin.Name}: rendering independent of soft-light behavior", strength == 28 && automatic);
                distinct.Add(((LinearGradientBrush)Application.Current.Resources["GemFill"]).GradientStops[1].Color);
                var image = Render(quickContent, Path.Combine(output, $"quick-{skin.Id}.png"));
                snapshots.Add(image);
            }
            Check("Four distinct complete palettes", distinct.Count == 4);
            RenderGallery(snapshots, Path.Combine(output, "skins.png"));
            main.SetSkin("jade");
            var root = (FrameworkElement)main.Content; main.Content = null;
            Render(root, Path.Combine(output, "main.png"), 960, 714);
        }
        finally { quick.Close(); main.DisposeForVerification(); }
        using (var sound = new InteractionSound { Enabled = false })
        {
            sound.Play("touch");
            Check("Muted interaction does not start playback", sound.Playback.IsCompleted && !sound.IsPlaying);
            if (audioPreview)
            {
                var elapsed = Stopwatch.StartNew(); sound.Preview();
                while (!sound.Playback.IsCompleted)
                {
                    var frame = new DispatcherFrame();
                    var tick = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Background, (_, _) => frame.Continue = false, Dispatcher.CurrentDispatcher);
                    Dispatcher.PushFrame(frame); tick.Stop();
                }
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));
                if (sound.LastError != null) checks.Add("DEVICE " + sound.LastError);
                else Check($"Preview plays through synchronous backend while muted ({elapsed.ElapsedMilliseconds} ms)", elapsed.ElapsedMilliseconds >= 100);
                Check("Preview preserves mute preference", !sound.Enabled);
            }
        }
        File.WriteAllLines(Path.Combine(output, "verification.txt"), checks);
    }

    private static bool HasFocusBorderTrigger(ControlTemplate template)
    {
        foreach (var item in template.Triggers)
            if (item is Trigger trigger && trigger.Property == UIElement.IsKeyboardFocusedProperty) return true;
        return false;
    }

    private static BitmapSource Render(FrameworkElement content, string path, double width = 308, double height = double.NaN)
    {
        var surface = new Border { Width = width, Background = new SolidColorBrush(Color.FromRgb(234, 238, 239)), Child = content };
        surface.Measure(new Size(width, double.IsNaN(height) ? double.PositiveInfinity : height));
        if (double.IsNaN(height)) height = Math.Ceiling(surface.DesiredSize.Height);
        surface.Height = height; surface.Arrange(new Rect(0, 0, width, height)); surface.UpdateLayout();
        var image = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32); image.Render(surface);
        Save(image, path); surface.Child = null; return image;
    }
    private static void RenderGallery(List<BitmapSource> images, string path)
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(239, 240, 239)), null, new Rect(0, 0, 1292, 422));
            for (int i = 0; i < images.Count; i++) drawing.DrawImage(images[i], new Rect(12 + i * 320, 12, images[i].PixelWidth, images[i].PixelHeight));
        }
        var gallery = new RenderTargetBitmap(1292, 422, 96, 96, PixelFormats.Pbgra32); gallery.Render(visual); Save(gallery, path);
    }
    private static void Save(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path); encoder.Save(file);
    }
}
