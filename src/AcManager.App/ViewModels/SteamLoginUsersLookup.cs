using AcTools.Platform.Linux;

namespace AcManager.App.ViewModels;

public interface ISteamIdLookup
{
    string? MostRecentId();

    string? PersonaName();
}

public sealed class SteamLoginUsersLookup : ISteamIdLookup
{
    public string? MostRecentId() => SteamLoginUsers.FindMostRecentId();

    public string? PersonaName() => SteamLoginUsers.FindAccount()?.PersonaName;
}
