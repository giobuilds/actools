using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace AcManager.Core.Bridge;

/// <summary>
/// Speaks the AcBridge protocol on 127.0.0.1. The Windows helper will feed it from the game's shared memory.
/// </summary>
public sealed class SessionBridgeHost : IDisposable
{
    private readonly int _physicsSize;
    private readonly int _graphicsSize;
    private readonly int _staticSize;
    private readonly TcpListener _listener;
    private readonly Channel<byte[]> _frames = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions {
        SingleReader = true,
    });
    private readonly CancellationTokenSource _cancel = new();
    private readonly Task _serve;

    public SessionBridgeHost(int? physicsSize = null, int? graphicsSize = null, int? staticSize = null)
    {
        _physicsSize = physicsSize ?? AcSharedLayout.PhysicsSize;
        _graphicsSize = graphicsSize ?? AcSharedLayout.GraphicsSize;
        _staticSize = staticSize ?? AcSharedLayout.StaticSize;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var endpoint = (IPEndPoint)_listener.LocalEndpoint;
        Address = endpoint.Address;
        Port = endpoint.Port;
        _serve = Task.Run(Serve);
    }

    public IPAddress Address { get; }

    public int Port { get; }

    public void Publish(byte type, int packetId, byte[] payload)
    {
        _frames.Writer.TryWrite(SessionBridgeProtocol.EncodeFrame(type, packetId, payload));
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
            using var client = await _listener.AcceptTcpClientAsync(_cancel.Token);
            client.NoDelay = true;
            await using var stream = client.GetStream();
            var hello = SessionBridgeProtocol.EncodeHello(_physicsSize, _graphicsSize, _staticSize);
            await stream.WriteAsync(hello, _cancel.Token);
            await foreach (var frame in _frames.Reader.ReadAllAsync(_cancel.Token)) {
                await stream.WriteAsync(frame, _cancel.Token);
            }
        } catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException or InvalidOperationException) {
        }
    }
}
