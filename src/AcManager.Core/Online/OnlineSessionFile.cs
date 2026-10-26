using System.Globalization;
using System.Text;
using AcManager.Core.Settings;

namespace AcManager.Core.Online;

public sealed record OnlineJoin(
    string Ip,
    int HttpPort,
    int RacePort,
    string ServerName,
    string CarId,
    string Password,
    string DriverName,
    string? SteamId,
    bool Extended = false,
    string? Features = null);

public static class OnlineSessionFile
{
    public static string Write(string documentsDirectory, OnlineJoin join)
    {
        if (!LobbyAddress.IsHost(join.Ip) || join.HttpPort is < 1 or > 65535) {
            throw new InvalidOperationException("Enter an address as ip:httpPort.");
        }

        if (join.RacePort is < 1 or > 65535) {
            throw new InvalidOperationException("This server has no race port.");
        }

        var carId = OneLine(join.CarId, 128);
        if (carId.Length == 0 || carId.Contains('/') || carId.Contains('\\')) {
            throw new InvalidOperationException("Pick a car.");
        }

        var path = CfgEdit.PathIn(documentsDirectory, "race.ini");
        var text = File.Exists(path) ? File.ReadAllText(path) : null;
        var guid = SteamIdText.IsSeventeenDigits(join.SteamId) ? join.SteamId! : "";
        text = CfgEdit.Apply(text, "REMOTE", [
            new KeyValuePair<string, string>("ACTIVE", "1"),
            new KeyValuePair<string, string>("SERVER_IP", join.Ip),
            new KeyValuePair<string, string>("SERVER_PORT", join.RacePort.ToString(CultureInfo.InvariantCulture)),
            new KeyValuePair<string, string>("SERVER_HTTP_PORT", join.HttpPort.ToString(CultureInfo.InvariantCulture)),
            new KeyValuePair<string, string>("SERVER_NAME", OneLine(join.ServerName, 200)),
            new KeyValuePair<string, string>("REQUESTED_CAR", carId),
            new KeyValuePair<string, string>("PASSWORD", OneLine(join.Password, 200)),
            new KeyValuePair<string, string>("NAME", OneLine(string.IsNullOrWhiteSpace(join.DriverName) ? "Player" : join.DriverName, 80)),
            new KeyValuePair<string, string>("GUID", guid),
            new KeyValuePair<string, string>("TEAM", ""),
            new KeyValuePair<string, string>("__CM_EXTENDED", join.Extended ? "1" : "0"),
        ]);
        var features = ServerSessionFlagsJson.Join(SplitFeatures(join.Features));
        if (features == null) {
            text = AcTools.Core.Data.IniText.RemoveKeys(text, "REMOTE", ["__FEATURES"]);
        } else {
            text = CfgEdit.Apply(text, "REMOTE", [
                new KeyValuePair<string, string>("__FEATURES", features),
            ]);
        }

        text = CfgEdit.Apply(text, "REPLAY", [
            new KeyValuePair<string, string>("ACTIVE", "0"),
        ]);
        CfgEdit.Commit(path, text);
        return path;
    }

    private static IEnumerable<string> SplitFeatures(string? features)
    {
        if (string.IsNullOrWhiteSpace(features)) {
            yield break;
        }

        foreach (var part in features.Split(',')) {
            yield return part.Trim();
        }
    }

    private static string OneLine(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || max <= 0) {
            return "";
        }

        var builder = new StringBuilder(Math.Min(value.Length, max));
        foreach (var c in value) {
            if (c is '\r' or '\n' or '\0') {
                continue;
            }

            builder.Append(c);
            if (builder.Length >= max) {
                break;
            }
        }

        return builder.ToString();
    }
}
