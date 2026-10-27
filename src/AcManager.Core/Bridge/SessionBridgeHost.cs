using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace AcManager.Core.Bridge;

public interface ISessionFrameSink
{
    void Publish(byte type, int packetId, ReadOnlySpan<byte> payload);
}

/// <summary>
/// Speaks the AcBridge protocol on 127.0.0.1. A client that leaves is dropped so the next visit can connect.
/// Each new client receives the latest physics, graphics, and static pages, including a page the previous socket already took.
/// </summary>
public sealed class SessionBridgeHost : ISessionFrameSink, IDisposable
{
    private const int QueueLength = 8;

    private readonly TcpListener _listener;
    private readonly byte[] _hello;
    private readonly Channel<byte[]> _frames = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(QueueLength) {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropOldest,
    });
    private readonly object _latestGate = new();
    private readonly byte[]?[] _latest = new byte[4][];
    private readonly CancellationTokenSource _cancel = new();
    private readonly Task _serve;

    public SessionBridgeHost(int? physicsSize = null, int? graphicsSize = null, int? staticSize = null, int port = 0)
    {
        _hello = SessionBridgeProtocol.EncodeHello(
            physicsSize ?? AcSharedLayout.PhysicsSize,
            graphicsSize ?? AcSharedLayout.GraphicsSize,
            staticSize ?? AcSharedLayout.StaticSize);
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        var endpoint = (IPEndPoint)_listener.LocalEndpoint;
        Address = endpoint.Address;
        Port = endpoint.Port;
        _serve = Task.Run(Serve);
    }

    public IPAddress Address { get; }

    public int Port { get; }

    public void Publish(byte type, int packetId, ReadOnlySpan<byte> payload)
    {
        var frame = SessionBridgeProtocol.EncodeFrame(type, packetId, payload);
        if (type is SessionBridgeProtocol.Physics or SessionBridgeProtocol.Graphics or SessionBridgeProtocol.Static) {
            lock (_latestGate) {
                _latest[type] = frame;
            }
        }

        _frames.Writer.TryWrite(frame);
    }

    public void Dispose()
    {
        _cancel.Cancel();
        _listener.Stop();
        _frames.Writer.TryComplete();
        try {
            _serve.Wait(TimeSpan.FromSeconds(2));
        } catch (AggregateException) {
        }

        _cancel.Dispose();
    }

    private async Task Serve()
    {
        try {
            while (!_cancel.IsCancellationRequested) {
                TcpClient client;
                try {
                    client = await _listener.AcceptTcpClientAsync(_cancel.Token);
                } catch (Exception ex) when (IsStop(ex)) {
                    break;
                }

                using (client) {
                    try {
                        await WriteTo(client);
                    } catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException) {
                    }
                }
            }
        } catch (Exception ex) when (IsStop(ex)) {
        }
    }

    private async Task WriteTo(TcpClient client)
    {
        client.NoDelay = true;
        await using var stream = client.GetStream();
        await stream.WriteAsync(_hello, _cancel.Token);
        foreach (var frame in LatestFrames()) {
            await stream.WriteAsync(frame, _cancel.Token);
        }

        while (!_cancel.IsCancellationRequested) {
            if (ClientLeft(client)) {
                return;
            }

            while (!ClientLeft(client) && _frames.Reader.TryRead(out var frame)) {
                await stream.WriteAsync(frame, _cancel.Token);
            }

            if (client.Client.Poll(0, SelectMode.SelectRead) && client.Client.Available == 0) {
                return;
            }

            await Task.Delay(20, _cancel.Token);
        }
    }

    private byte[][] LatestFrames()
    {
        lock (_latestGate) {
            var count = 0;
            for (var i = 0; i < _latest.Length; i++) {
                if (_latest[i] != null) {
                    count++;
                }
            }

            var frames = new byte[count][];
            var index = 0;
            for (var i = 0; i < _latest.Length; i++) {
                var frame = _latest[i];
                if (frame != null) {
                    frames[index++] = frame;
                }
            }

            return frames;
        }
    }

    private static bool ClientLeft(TcpClient client)
    {
        try {
            return client.Client.Poll(0, SelectMode.SelectRead) && client.Client.Available == 0;
        } catch (Exception ex) when (ex is SocketException or ObjectDisposedException) {
            return true;
        }
    }

    private static bool IsStop(Exception ex) =>
        ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException or InvalidOperationException;
}
