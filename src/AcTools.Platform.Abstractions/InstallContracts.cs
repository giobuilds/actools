namespace AcTools.Platform;

public sealed class InstallSettings
{
    public string? GameRoot { get; set; }

    public string? DocumentsDirectory { get; set; }

    public string? ProtonPrefix { get; set; }
}

public interface IInstallLocator
{
    AcInstallLayout Locate();

    IReadOnlyList<string> DefaultSteamRoots { get; }
}

public interface IInstallSettingsStore
{
    InstallSettings Load();

    void Save(InstallSettings settings);

    /// <summary>
    /// Saved overrides win. Empty overrides fall through to <paramref name="locator"/>.
    /// A saved game root still gets its Proton prefix and documents filled in when those boxes are empty.
    /// </summary>
    AcInstallLayout Resolve(IInstallLocator locator);
}

public enum GameLaunchStatus
{
    SteamInvoked,
    SteamNotFound,
}

public interface IGameLauncher
{
    GameLaunchStatus LaunchAssettoCorsa();

    bool IsGameRunning();
}
