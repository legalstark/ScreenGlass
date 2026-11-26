using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ScreenGlass;

internal static class ReleaseUpdates
{
    private const string Repository = "legalstark/ScreenGlass";
    internal static Version CurrentVersion { get; } = Version.Parse(typeof(ReleaseUpdates).Assembly.GetName().Version!.ToString(3));

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

    static ReleaseUpdates()
    {
        Client.DefaultRequestHeaders.UserAgent.ParseAdd($"ScreenGlass/{CurrentVersion}");
        Client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    internal static async Task<Version?> GetLatestVersionAsync()
    {
        using var response = await Client.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest").ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var release = await response.Content.ReadFromJsonAsync<Release>().ConfigureAwait(false);
        if (release?.TagName is not { } tag || !tag.StartsWith('v')
            || !Version.TryParse(tag.AsSpan(1), out var version) || version.Build < 0
            || version.Revision >= 0 || tag != $"v{version}")
        {
            throw new JsonException("The release tag must have the format vMAJOR.MINOR.PATCH.");
        }

        return version;
    }

    internal static string GetReleasePage(Version version) => $"https://github.com/{Repository}/releases/tag/v{version}";

    private sealed record Release([property: JsonPropertyName("tag_name")] string? TagName);
}
