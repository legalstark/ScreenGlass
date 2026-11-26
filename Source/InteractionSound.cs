using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ScreenGlass;

internal sealed class InteractionSound : IDisposable
{
    private readonly Dictionary<string, SoundPlayer> players = new();
    private readonly List<Stream> streams = new();
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private bool disposed;
    private volatile bool playing;
    public bool Enabled { get; set; } = true;
    public bool IsPlaying => playing;
    public string? LastError { get; private set; }
    public Task Playback { get; private set; } = Task.CompletedTask;
    public event Action? StatusChanged;

    public void Play(string cue) => Start(cue, false);
    public void Preview() => Start("on", true);
    private void Start(string cue, bool preview)
    {
        if (disposed || playing || (!Enabled && !preview)) return;
        if (waveOutGetNumDevs() == 0)
        {
            LastError = "当前没有可用音频输出设备，请连接扬声器或耳机后重试。";
            StatusChanged?.Invoke();
            return;
        }
        try
        {
            if (!players.TryGetValue(cue, out var player))
            {
                var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/ScreenGlass;component/Assets/Sounds/{cue}.wav"))!.Stream;
                streams.Add(stream);
                player = new SoundPlayer(stream);
                player.Load();
                players.Add(cue, player);
            }
            LastError = null;
            playing = true;
            StatusChanged?.Invoke();
            Playback = Task.Run(() =>
            {
                try { player.PlaySync(); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException)
                { LastError = "无法播放音效，请检查输出设备或应用音量。"; }
                finally
                {
                    playing = false;
                    if (!dispatcher.HasShutdownStarted)
                        dispatcher.BeginInvoke(new Action(() => { if (!disposed) StatusChanged?.Invoke(); }));
                }
            });
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException)
        {
            LastError = "音效未能加载，请点击试听重试。";
            StatusChanged?.Invoke();
        }
    }
    public void Dispose()
    {
        disposed = true;
        foreach (var player in players.Values) player.Stop();
        // Release buffers only after a synchronous worker has finished reading them.
        if (Playback.IsCompleted) Release();
        else _ = Playback.ContinueWith(_ => Release(), TaskScheduler.Default);
    }
    [DllImport("winmm.dll")] private static extern uint waveOutGetNumDevs();

    private void Release()
    {
        foreach (var player in players.Values) player.Dispose();
        foreach (var stream in streams) stream.Dispose();
    }
}
