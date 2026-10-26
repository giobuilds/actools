using System.Buffers.Binary;

namespace AcManager.Core.Bridge;

public static class SharedPageSchedule
{
    public const int LivePhysicsMs = 50;

    public const int LiveGraphicsMs = 100;

    public const int IdleMs = 200;

    public const int DisconnectedMs = 2000;
}

/// <summary>
/// Decides which shared-memory pages are due. Physics and graphics slow down when their packet id stays put.
/// Static is sent when its bytes change. The sink must copy the payload before it returns.
/// </summary>
public sealed class SharedPagePump
{
    private readonly ISharedPages _pages;
    private readonly ISessionFrameSink _sink;
    private readonly byte[] _physics = new byte[AcSharedLayout.PhysicsSize];
    private readonly byte[] _graphics = new byte[AcSharedLayout.GraphicsSize];
    private readonly byte[] _page = new byte[AcSharedLayout.StaticSize];
    private int? _physicsSentId;
    private int? _graphicsSentId;
    private long _physicsSentAt = -1;
    private long _graphicsSentAt = -1;
    private byte[]? _sentStatic;
    private int _staticPacketId;

    public SharedPagePump(ISharedPages pages, ISessionFrameSink sink)
    {
        _pages = pages;
        _sink = sink;
    }

    public int Tick(long nowMs)
    {
        var next = int.MaxValue;
        var sawTimed = false;
        if (_pages.TryCopy(SessionBridgeProtocol.Physics, _physics)) {
            sawTimed = true;
            next = Math.Min(next, Consider(nowMs, _physics, SessionBridgeProtocol.Physics, SharedPageSchedule.LivePhysicsMs, ref _physicsSentId, ref _physicsSentAt));
        }

        if (_pages.TryCopy(SessionBridgeProtocol.Graphics, _graphics)) {
            sawTimed = true;
            next = Math.Min(next, Consider(nowMs, _graphics, SessionBridgeProtocol.Graphics, SharedPageSchedule.LiveGraphicsMs, ref _graphicsSentId, ref _graphicsSentAt));
        }

        if (_pages.TryCopy(SessionBridgeProtocol.Static, _page)
            && (_sentStatic == null || !_sentStatic.AsSpan().SequenceEqual(_page))) {
            _staticPacketId++;
            _sink.Publish(SessionBridgeProtocol.Static, _staticPacketId, _page);
            _sentStatic = (byte[])_page.Clone();
        }

        return sawTimed ? next : SharedPageSchedule.DisconnectedMs;
    }

    private int Consider(long nowMs, byte[] page, byte type, int liveInterval, ref int? sentId, ref long sentAt)
    {
        var id = BinaryPrimitives.ReadInt32LittleEndian(page);
        var moving = sentId is int previous && previous != id;
        var interval = moving ? liveInterval : SharedPageSchedule.IdleMs;
        if (sentAt < 0 || nowMs - sentAt >= interval) {
            _sink.Publish(type, id, page);
            sentId = id;
            sentAt = nowMs;
        }

        return Remaining(nowMs, sentAt, interval);
    }

    private static int Remaining(long nowMs, long sentAt, int interval)
    {
        var left = interval - (nowMs - sentAt);
        if (left < 1) {
            return 1;
        }

        return left > int.MaxValue ? int.MaxValue : (int)left;
    }
}
