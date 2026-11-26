using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ScreenGlass;

public partial class MainWindow
{
    private async void CheckForUpdates(Forms.ToolStripItem item)
    {
        if (exiting || !item.Enabled) return;
        item.Enabled = false;
        item.Text = "正在检查更新…";
        var current = ReleaseUpdates.CurrentVersion;

        try
        {
            var latest = await ReleaseUpdates.GetLatestVersionAsync();
            if (exiting) return;

            if (latest is null)
                ShowUpdateMessage($"GitHub 暂无正式发布。当前版本：{current}。");
            else if (latest <= current)
                ShowUpdateMessage($"当前版本 {current} 已是最新版本。");
            else if (ShowUpdateMessage($"发现新版本 {latest}，当前版本为 {current}。\n\n打开 GitHub 发布页下载？",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using var browser = Process.Start(new ProcessStartInfo(ReleaseUpdates.GetReleasePage(latest)) { UseShellExecute = true });
            }
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode is 403 or 429)
        {
            if (!exiting) ShowUpdateMessage("GitHub 暂时限制了版本查询，请稍后重试。");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            if (!exiting) ShowUpdateMessage("暂时无法从 GitHub 获取版本，请检查网络后重试。");
        }
        catch (JsonException)
        {
            if (!exiting) ShowUpdateMessage("GitHub 发布的版本号格式有误，暂时无法检查更新。");
        }
        catch (Win32Exception)
        {
            if (!exiting) ShowUpdateMessage("无法打开默认浏览器。请检查 Windows 的默认浏览器设置。");
        }
        finally
        {
            if (!exiting)
            {
                item.Text = "检查更新…";
                item.Enabled = true;
            }
        }
    }

    private MessageBoxResult ShowUpdateMessage(string text, MessageBoxButton buttons = MessageBoxButton.OK) => IsVisible
        ? MessageBox.Show(this, text, "ScreenGlass · 检查更新", buttons, MessageBoxImage.Information)
        : MessageBox.Show(text, "ScreenGlass · 检查更新", buttons, MessageBoxImage.Information);
}
