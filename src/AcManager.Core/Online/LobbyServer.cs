using System.Globalization;

namespace AcManager.Core.Online;

/// <summary>
/// One lobby entry. RacePort is JSON tport and is written to race.ini SERVER_PORT.
/// HttpPort is JSON cport. The JSON port field is only a lobby query argument and is not kept.
/// </summary>
public sealed record LobbyServer(
    string Ip,
    int HttpPort,
    int RacePort,
    string Name,
    string Track,
    int Players,
    int Capacity,
    bool PasswordRequired,
    IReadOnlyList<string> Cars)
{
    public string Address => Ip + ":" + HttpPort.ToString(CultureInfo.InvariantCulture);

    public string Title => string.IsNullOrWhiteSpace(Name) ? Address : Name;

    public string PlayersLabel =>
        Players.ToString(CultureInfo.InvariantCulture) + "/" + Capacity.ToString(CultureInfo.InvariantCulture);

    public string Subtitle
    {
        get {
            var track = string.IsNullOrWhiteSpace(Track) ? "unknown track" : Track;
            var password = PasswordRequired ? "  ·  password" : "";
            return track + "  ·  " + PlayersLabel + password + "  ·  " + Address;
        }
    }
}
