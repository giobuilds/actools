namespace AcManager.Core.Online;

public interface IOnlineDirectory
{
    Task<LobbyFetch> LoadListAsync(CancellationToken cancellationToken);

    Task<LobbyFetch> LoadServerAsync(string ip, int httpPort, CancellationToken cancellationToken);

    Task<DetailsFetch> LoadDetailsAsync(string ip, int httpPort, CancellationToken cancellationToken);
}

public sealed class DetailsFetch
{
    private DetailsFetch(string? json, string? error)
    {
        Json = json;
        Error = error;
    }

    public string? Json { get; }

    public string? Error { get; }

    public bool Ok => Error == null;

    public static DetailsFetch Success(string json) => new(json, null);

    public static DetailsFetch Failure(string error) => new(null, error);
}

public sealed class LobbyFetch
{
    private LobbyFetch(IReadOnlyList<LobbyServer> servers, string? error)
    {
        Servers = servers;
        Error = error;
    }

    public IReadOnlyList<LobbyServer> Servers { get; }

    public string? Error { get; }

    public bool Ok => Error == null;

    public static LobbyFetch Success(IReadOnlyList<LobbyServer> servers) => new(servers, null);

    public static LobbyFetch Failure(string error) => new([], error);
}
