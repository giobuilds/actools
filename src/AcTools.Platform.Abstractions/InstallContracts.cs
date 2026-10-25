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
    /// <summary>acs.exe was started under the prefix's Proton build.</summary>
    SessionStarted,

    /// <summary>The layout has no acs.exe to start.</summary>
    GameNotFound,

    /// <summary>The prefix does not point at a Proton build we can run.</summary>
    ProtonNotFound,

    /// <summary>Proton was found, but the process did not start.</summary>
    LaunchFailed,
}

public interface IGameLauncher
{
    /// <summary>
    /// Starts acs.exe in the layout's Proton prefix. steam -applaunch starts the official launcher menu instead.
    /// </summary>
    GameLaunchStatus LaunchSession(AcInstallLayout layout);

    bool IsGameRunning();
}
