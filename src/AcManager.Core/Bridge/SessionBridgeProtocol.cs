using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace AcManager.Core.Bridge;

public interface ISessionBridge
{
    string ReadNote();
}

public static class SessionBridgeProtocol
{
    public const int Version = 1;

    public const int DefaultPort = 27150;

    public const byte Physics = 1;

    public const byte Graphics = 2;

    public const byte Static = 3;

    public const string OffNote = "Live timing is off. AcBridge is not connected.";

    public const string SizeNote = "AcBridge sent an unexpected shared-memory size.";

    public const string WaitingNote = "Live timing is connected. No shared-memory frames yet.";

    public static byte[] EncodeHello(int physicsSize, int graphicsSize, int staticSize)
    {
        var bytes = new byte[20];
        "ACB1"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), Version);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), physicsSize);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), graphicsSize);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), staticSize);
        return bytes;
    }

    public static byte[] EncodeFrame(byte type, int packetId, ReadOnlySpan<byte> payload)
    {
        var bytes = new byte[9 + payload.Length];
        bytes[0] = type;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1), packetId);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(5), payload.Length);
        payload.CopyTo(bytes.AsSpan(9));
        return bytes;
    }

    public static string Text(ReadOnlySpan<byte> page, int offset, int characters)
    {
        if (offset < 0 || characters < 0 || offset + (characters * 2) > page.Length) {
            return "";
        }

        var text = Encoding.Unicode.GetString(page.Slice(offset, characters * 2));
        var end = text.IndexOf('\0');
        return (end >= 0 ? text[..end] : text).Trim();
    }

    /// <summary>
    /// The game stores reverse as 0, neutral as 1, and first gear as 2.
    /// </summary>
    public static string GearLabel(int gear) => gear switch {
        0 => "R",
        1 => "N",
        _ => (gear - 1).ToString(CultureInfo.InvariantCulture),
    };

    public static string Note(int? laps, int? position, int? gear, float? speedKmh, string? car, string? track)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(car) && !string.IsNullOrWhiteSpace(track)) {
            parts.Add(car + " on " + track);
        } else if (!string.IsNullOrWhiteSpace(car)) {
            parts.Add(car);
        } else if (!string.IsNullOrWhiteSpace(track)) {
            parts.Add(track);
        }

        if (laps is int completed) {
            parts.Add(completed.ToString(CultureInfo.InvariantCulture) + " laps completed");
        }

        if (position is int place) {
            parts.Add("position " + place.ToString(CultureInfo.InvariantCulture));
        }

        if (gear is int selected) {
            parts.Add("gear " + GearLabel(selected));
        }

        if (speedKmh is float speed) {
            parts.Add(speed.ToString("0.#", CultureInfo.InvariantCulture) + " km/h");
        }

        return parts.Count == 0 ? WaitingNote : "Live timing: " + string.Join(", ", parts) + ".";
    }
}
