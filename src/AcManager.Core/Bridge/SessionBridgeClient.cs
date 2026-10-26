using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace AcManager.Core.Bridge;

public sealed class SessionBridgeClient : ISessionBridge
{
    private readonly int _port;
    private readonly TimeSpan _connectTimeout;
    private readonly TimeSpan _readIdle;

    public SessionBridgeClient(int port = SessionBridgeProtocol.DefaultPort, TimeSpan? connectTimeout = null, TimeSpan? readIdle = null)
    {
        _port = port;
        _connectTimeout = connectTimeout ?? TimeSpan.FromMilliseconds(200);
        _readIdle = readIdle ?? TimeSpan.FromMilliseconds(100);
    }

    public string ReadNote()
    {
        using var client = new TcpClient();
        using var connectCancel = new CancellationTokenSource(_connectTimeout);
        try {
            client.ConnectAsync(IPAddress.Loopback, _port, connectCancel.Token).AsTask().GetAwaiter().GetResult();
            client.NoDelay = true;
            using var stream = client.GetStream();
            stream.ReadTimeout = (int)_connectTimeout.TotalMilliseconds;
            var hello = new byte[20];
            if (!TryRead(stream, hello) || !"ACB1"u8.SequenceEqual(hello.AsSpan(0, 4))) {
                return SessionBridgeProtocol.SizeNote;
            }

            var version = BinaryPrimitives.ReadInt32LittleEndian(hello.AsSpan(4));
            var physicsSize = BinaryPrimitives.ReadInt32LittleEndian(hello.AsSpan(8));
            var graphicsSize = BinaryPrimitives.ReadInt32LittleEndian(hello.AsSpan(12));
            var staticSize = BinaryPrimitives.ReadInt32LittleEndian(hello.AsSpan(16));
            if (version != SessionBridgeProtocol.Version
                || physicsSize != AcSharedLayout.PhysicsSize
                || graphicsSize != AcSharedLayout.GraphicsSize
                || staticSize != AcSharedLayout.StaticSize) {
                return SessionBridgeProtocol.SizeNote;
            }

            stream.ReadTimeout = (int)Math.Max(1, _readIdle.TotalMilliseconds);
            int? laps = null;
            int? position = null;
            int? gear = null;
            float? speed = null;
            string? car = null;
            string? track = null;
            var sawFrame = false;
            var header = new byte[9];
            while (TryRead(stream, header)) {
                var type = header[0];
                var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(5));
                var expected = Expected(type, physicsSize, graphicsSize, staticSize);
                if (expected == null || length != expected.Value) {
                    return SessionBridgeProtocol.SizeNote;
                }

                var payload = new byte[length];
                if (!TryRead(stream, payload)) {
                    break;
                }

                sawFrame = true;
                if (type == SessionBridgeProtocol.Physics) {
                    gear = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(AcSharedLayout.GearOffset));
                    speed = BinaryPrimitives.ReadSingleLittleEndian(payload.AsSpan(AcSharedLayout.SpeedOffset));
                } else if (type == SessionBridgeProtocol.Graphics) {
                    laps = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(AcSharedLayout.CompletedLapsOffset));
                    position = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(AcSharedLayout.PositionOffset));
                } else if (type == SessionBridgeProtocol.Static) {
                    car = SessionBridgeProtocol.Text(payload, AcSharedLayout.CarModelOffset, AcSharedLayout.IdCharacters);
                    track = SessionBridgeProtocol.Text(payload, AcSharedLayout.TrackOffset, AcSharedLayout.IdCharacters);
                }
            }

            return sawFrame
                ? SessionBridgeProtocol.Note(laps, position, gear, speed, car, track)
                : SessionBridgeProtocol.WaitingNote;
        } catch (Exception ex) when (ex is OperationCanceledException or SocketException or IOException or ObjectDisposedException) {
            return SessionBridgeProtocol.OffNote;
        }
    }

    private static int? Expected(byte type, int physicsSize, int graphicsSize, int staticSize) => type switch {
        SessionBridgeProtocol.Physics => physicsSize,
        SessionBridgeProtocol.Graphics => graphicsSize,
        SessionBridgeProtocol.Static => staticSize,
        _ => null,
    };

    private static bool TryRead(NetworkStream stream, Span<byte> buffer)
    {
        var read = 0;
        while (read < buffer.Length) {
            int count;
            try {
                count = stream.Read(buffer[read..]);
            } catch (IOException) {
                return false;
            }

            if (count == 0) {
                return false;
            }

            read += count;
        }

        return true;
    }
}
