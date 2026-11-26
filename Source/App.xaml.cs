using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
namespace ScreenGlass;
public partial class App : Application
{
    private Mutex? singleton;
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!Environment.Is64BitProcess || !WindowsSupport.IsSupported(Environment.OSVersion.Version))
        {
            MessageBox.Show(WindowsSupport.Requirement, "ScreenGlass");
            Shutdown(1);
            return;
        }
        if (e.Args.Contains("--adaptive-test") || e.Args.Contains("--dimming-test") || e.Args.Contains("--self-test") || e.Args.Contains("--polish-test") || e.Args.Contains("--platform-test"))
        {
            string output = Path.Combine(AppContext.BaseDirectory, "verification");
            int option = Array.IndexOf(e.Args, "--output");
            if (option >= 0 && option + 1 < e.Args.Length) output = Path.GetFullPath(e.Args[option + 1]);
            base.OnStartup(e);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (e.Args.Contains("--adaptive-test")) AdaptiveVerification.Run(output);
                    else if (e.Args.Contains("--dimming-test")) DimmingVerification.Run(output);
                    else if (e.Args.Contains("--platform-test")) PlatformVerification.Run(output);
                    else if (e.Args.Contains("--polish-test")) PolishVerification.Run(output, e.Args.Contains("--audio-preview"));
                    else ReleaseVerification.Run(output, e.Args.Contains("--window-smoke"));
                    Shutdown(0);
                }
                catch (Exception ex)
                {
                    Directory.CreateDirectory(output);
                    File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString());
                    Shutdown(1);
                }
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            return;
        }
        singleton = new Mutex(true, "Local\\ScreenGlass.Desktop.v1", out var first);
        if (!first)
        {
            var existing = FindWindow(null, "ScreenGlass");
            if (existing != IntPtr.Zero) PostMessage(existing, 0x8001, IntPtr.Zero, IntPtr.Zero);
            Shutdown();
            return;
        }
        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e) { singleton?.Dispose(); base.OnExit(e); }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string title);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam);
}
