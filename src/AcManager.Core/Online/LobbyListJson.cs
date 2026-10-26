using System.Globalization;
using System.Text.Json;

namespace AcManager.Core.Online;

public static class LobbyListJson
{
    public static bool TryParseList(string? json, out IReadOnlyList<LobbyServer> servers, out string error)
    {
        servers = [];
        error = "The lobby list was not a server list.";
        if (!TryDocument(json, JsonValueKind.Array, out var document)) {
            return false;
        }

        using (document) {
            var list = new List<LobbyServer>();
            foreach (var item in document.RootElement.EnumerateArray()) {
                if (TryReadServer(item, null, 0, out var server)) {
                    list.Add(server);
                }
            }

            servers = list;
            error = "";
            return true;
        }
    }

    public static bool TryParseServer(string? json, string fallbackIp, int fallbackHttpPort, out LobbyServer? server, out string error)
    {
        server = null;
        error = "The server did not return session info.";
        if (!TryDocument(json, JsonValueKind.Object, out var document)) {
            return false;
        }

        using (document) {
            if (!TryReadServer(document.RootElement, fallbackIp, fallbackHttpPort, out var read)) {
                return false;
            }

            server = read;
            error = "";
            return true;
        }
    }

    private static bool TryDocument(string? json, JsonValueKind kind, out JsonDocument document)
    {
        document = null!;
        if (string.IsNullOrWhiteSpace(json)) {
            return false;
        }

        var trimmed = json.Trim().TrimStart('\uFEFF');
        if (trimmed.Length == 0 || trimmed[0] != (kind == JsonValueKind.Array ? '[' : '{')) {
            return false;
        }

        try {
            document = JsonDocument.Parse(trimmed);
        } catch (JsonException) {
            return false;
        }

        if (document.RootElement.ValueKind != kind) {
            document.Dispose();
            return false;
        }

        return true;
    }

    private static bool TryReadServer(JsonElement item, string? fallbackIp, int fallbackHttpPort, out LobbyServer server)
    {
        server = null!;
        if (item.ValueKind != JsonValueKind.Object) {
            return false;
        }

        var ip = ReadString(item, "ip");
        if (!string.IsNullOrEmpty(fallbackIp)) {
            // A direct /INFO response can report a private address. The address that answered is the one to join.
            ip = fallbackIp;
        }

        var httpPort = ReadInt(item, "cport");
        if (httpPort <= 0) {
            httpPort = fallbackHttpPort;
        }

        if (string.IsNullOrWhiteSpace(ip) || !LobbyAddress.IsHost(ip) || httpPort is < 1 or > 65535) {
            return false;
        }

        var racePort = ReadInt(item, "tport");
        if (racePort is < 0 or > 65535) {
            racePort = 0;
        }

        server = new LobbyServer(
            ip,
            httpPort,
            racePort,
            ReadString(item, "name"),
            ReadString(item, "track"),
            Math.Max(0, ReadInt(item, "clients")),
            Math.Max(0, ReadInt(item, "maxclients")),
            ReadBool(item, "pass"),
            ReadCars(item));
        return true;
    }

    private static string ReadString(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value)) {
            return "";
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    }

    private static int ReadInt(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value)) {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) {
            return parsed;
        }

        return 0;
    }

    private static bool ReadBool(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value)) {
            return false;
        }

        if (value.ValueKind == JsonValueKind.True) {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False) {
            return false;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) {
            return number != 0;
        }

        if (value.ValueKind == JsonValueKind.String) {
            var text = value.GetString();
            return text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static IReadOnlyList<string> ReadCars(JsonElement item)
    {
        if (!item.TryGetProperty("cars", out var value) || value.ValueKind != JsonValueKind.Array) {
            return [];
        }

        var cars = new List<string>();
        foreach (var car in value.EnumerateArray()) {
            if (car.ValueKind != JsonValueKind.String) {
                continue;
            }

            var id = car.GetString();
            if (!string.IsNullOrWhiteSpace(id)) {
                cars.Add(id);
            }
        }

        return cars;
    }
}
