using System.Reflection;
using System.Text.Json;
using AcManager.Core.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcManager.App.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    public const string ReleasesUrl = "https://github.com/giobuilds/actools/releases";

    public string ReleasesPage => ReleasesUrl;

    private readonly IReleaseLookup _releases;

    public AboutViewModel(IReleaseLookup? releases = null)
    {
        _releases = releases ?? new GitHubReleaseLookup();
        LicenseText = ReadEmbedded("LICENSE.txt");
        NoticeText = ReadEmbedded("NOTICE.txt");
    }

    public string Version => AppVersion.Current;

    public string LicenseText { get; }

    public string NoticeText { get; }

    [ObservableProperty]
    private string _updateNote = "";

    [RelayCommand]
    public Task Check() => CheckAsync();

    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        UpdateNote = "Checking for updates.";
        try {
            var json = await _releases.ReadReleasesAsync(cancellationToken);
            if (!ReleaseVersions.TryNewerStable(Version, json, out var newer)) {
                UpdateNote = "Update check did not reach GitHub.";
                return;
            }

            UpdateNote = newer == null
                ? "This is the latest release."
                : "Version " + newer + " is available.";
        } catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException) {
            UpdateNote = "Update check did not reach GitHub.";
        }
    }

    private static string ReadEmbedded(string fileName)
    {
        var assembly = typeof(AboutViewModel).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(candidate => candidate.EndsWith(fileName, StringComparison.Ordinal));
        if (name == null) {
            return "";
        }

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream == null) {
            return "";
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
