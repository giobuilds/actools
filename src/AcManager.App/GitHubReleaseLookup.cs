namespace AcManager.App;

public interface IReleaseLookup
{
    Task<string> ReadReleasesAsync(CancellationToken cancellationToken);
}

public sealed class GitHubReleaseLookup : IReleaseLookup
{
    public const string ReleasesUrl = "https://api.github.com/repos/giobuilds/actools/releases?per_page=20";

    public async Task<string> ReadReleasesAsync(CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("actools-content-manager");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return await http.GetStringAsync(ReleasesUrl, cancellationToken);
    }
}
