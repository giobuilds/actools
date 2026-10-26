using System.Text;

namespace AcTools.Platform.Linux;

/// <summary>
/// A Steam account read from the local loginusers.vdf. Nothing here is a fixed id.
/// </summary>
public readonly record struct SteamAccount(string Id, string? PersonaName);

/// <summary>
/// Reads a Steam account from loginusers.vdf on this machine.
/// A 17-digit account marked MostRecent 1 wins.
/// Otherwise an AutoLogin 1 account is used, then the only 17-digit account
/// that is not marked MostRecent 0.
/// The search covers the same Steam roots as game discovery, including Flatpak and Snap.
/// </summary>
public static class SteamLoginUsers
{
    private readonly record struct AccountMark(
        string Id,
        string? PersonaName,
        bool MostRecent,
        bool AutoLogin,
        bool ExplicitlyNotRecent);

    public static string? ReadMostRecentId(string? vdf) => ReadAccount(vdf)?.Id;

    public static SteamAccount? ReadAccount(string? vdf)
    {
        if (string.IsNullOrEmpty(vdf)) {
            return null;
        }

        var tokens = Tokenize(vdf);
        var accounts = new List<AccountMark>();
        string? pendingKey = null;
        string? currentUser = null;
        string? personaName = null;
        var mostRecent = false;
        var autoLogin = false;
        var explicitlyNotRecent = false;
        var depth = 0;
        var userDepth = -1;
        foreach (var token in tokens) {
            if (token == "{") {
                depth++;
                if (pendingKey != null && IsSteamId(pendingKey) && userDepth < 0) {
                    currentUser = pendingKey;
                    userDepth = depth;
                    personaName = null;
                    mostRecent = false;
                    autoLogin = false;
                    explicitlyNotRecent = false;
                }

                pendingKey = null;
                continue;
            }

            if (token == "}") {
                if (userDepth == depth && currentUser != null) {
                    accounts.Add(new AccountMark(currentUser, personaName, mostRecent, autoLogin, explicitlyNotRecent));
                    currentUser = null;
                    userDepth = -1;
                }

                if (depth > 0) {
                    depth--;
                }

                pendingKey = null;
                continue;
            }

            if (pendingKey == null) {
                pendingKey = token;
                continue;
            }

            if (currentUser != null && depth == userDepth) {
                if (pendingKey == "MostRecent") {
                    mostRecent = token == "1";
                    explicitlyNotRecent = token == "0";
                } else if (pendingKey == "AutoLogin") {
                    autoLogin = token == "1";
                } else if (pendingKey == "PersonaName") {
                    personaName = token;
                }
            }

            pendingKey = null;
        }

        AccountMark? recent = null;
        AccountMark? automatic = null;
        foreach (var account in accounts) {
            if (account.MostRecent && recent == null) {
                recent = account;
            }

            if (account.AutoLogin && automatic == null) {
                automatic = account;
            }
        }

        AccountMark? chosen = recent ?? automatic;
        if (chosen == null && accounts.Count == 1 && !accounts[0].ExplicitlyNotRecent) {
            chosen = accounts[0];
        }

        if (chosen == null) {
            return null;
        }

        var name = chosen.Value.PersonaName;
        if (string.IsNullOrWhiteSpace(name)) {
            name = null;
        }

        return new SteamAccount(chosen.Value.Id, name);
    }

    public static string? FindMostRecentId() => FindAccount()?.Id;

    public static SteamAccount? FindAccount()
    {
        foreach (var path in CandidatePaths()) {
            if (!File.Exists(path)) {
                continue;
            }

            try {
                var account = ReadAccount(File.ReadAllText(path));
                if (account != null) {
                    return account;
                }
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                // The next Steam path may still be readable.
            }
        }

        return null;
    }

    public static IEnumerable<string> CandidatePaths()
    {
        foreach (var root in SteamAcDiscovery.GetDefaultSteamRoots()) {
            yield return Path.Combine(root, "config", "loginusers.vdf");
        }
    }

    private static bool IsSteamId(string value)
    {
        if (value.Length != 17) {
            return false;
        }

        foreach (var c in value) {
            if (c is < '0' or > '9') {
                return false;
            }
        }

        return true;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        for (var i = 0; i < text.Length; i++) {
            var c = text[i];
            if (char.IsWhiteSpace(c)) {
                continue;
            }

            if (c is '{' or '}') {
                tokens.Add(c.ToString());
                continue;
            }

            if (c != '"') {
                continue;
            }

            var value = new StringBuilder();
            for (i++; i < text.Length; i++) {
                var inner = text[i];
                if (inner == '\\' && i + 1 < text.Length) {
                    value.Append(text[i + 1]);
                    i++;
                    continue;
                }

                if (inner == '"') {
                    break;
                }

                value.Append(inner);
            }

            tokens.Add(value.ToString());
        }

        return tokens;
    }
}
