using System.Buffers.Binary;
using AcManager.Core.Bridge;
using AcTools.Platform.Linux;
using Xunit;

namespace AcManager.Core.Tests;

public class SharedPagePumpTests
{
    [Fact]
    public void Stage_keeps_the_prefix_copy_when_it_is_already_current()
    {
        var root = Directory.CreateTempSubdirectory("actools-bridge-stage-").FullName;
        try {
            var source = Path.Combine(root, "AcBridge.exe");
            var compat = Path.Combine(root, "compat");
            Directory.CreateDirectory(compat);
            File.WriteAllBytes(source, [1, 2, 3, 4]);
            var staged = AcBridgeFiles.Stage(source, compat);

            Assert.Equal(AcBridgeFiles.PrefixExecutable(compat), staged);
            Assert.NotNull(staged);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(staged));
            var written = File.GetLastWriteTimeUtc(staged);
            Assert.Equal(staged, AcBridgeFiles.Stage(source, compat));
            Assert.Equal(written, File.GetLastWriteTimeUtc(staged));

            File.WriteAllBytes(source, [5, 6]);
            File.SetLastWriteTimeUtc(source, written.AddMinutes(1));
            AcBridgeFiles.Stage(source, compat);
            Assert.Equal(new byte[] { 5, 6 }, File.ReadAllBytes(staged));
            Assert.Null(AcBridgeFiles.Stage(Path.Combine(root, "missing.exe"), compat));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Helper_port_matches_the_protocol()
    {
        Assert.Equal(SessionBridgeProtocol.DefaultPort, AcBridgeFiles.Port);
        Assert.Equal(27150, BridgeArguments.Port([]));
        Assert.Equal(27151, BridgeArguments.Port(["--port", "27151"]));
        Assert.Equal(SessionBridgeProtocol.DefaultPort, BridgeArguments.Port(["--port", "0"]));
        Assert.Equal(SessionBridgeProtocol.DefaultPort, BridgeArguments.Port(["--port"]));
    }

    [Fact]
    public void Shared_memory_names_are_the_game_mappings()
    {
        Assert.Equal(@"Local\acpmf_physics", WindowsSharedPages.Name(SessionBridgeProtocol.Physics));
        Assert.Equal(@"Local\acpmf_graphics", WindowsSharedPages.Name(SessionBridgeProtocol.Graphics));
        Assert.Equal(@"Local\acpmf_static", WindowsSharedPages.Name(SessionBridgeProtocol.Static));
        Assert.Null(WindowsSharedPages.Name(9));
    }

    [Fact]
    public void Missing_mapping_is_tried_again_on_the_next_read()
    {
        var opens = 0;
        var payload = new byte[] { 9, 8, 7, 6 };
        using var pages = new WindowsSharedPages(_ => {
            opens++;
            if (opens == 1) {
                throw new FileNotFoundException();
            }

            return opens == 2 ? null : new BytesPage(payload);
        });
        var buffer = new byte[4];

        Assert.False(pages.TryCopy(SessionBridgeProtocol.Physics, buffer));
        Assert.False(pages.TryCopy(SessionBridgeProtocol.Physics, buffer));
        Assert.True(pages.TryCopy(SessionBridgeProtocol.Physics, buffer));
        Assert.Equal(payload, buffer);
        Assert.True(pages.TryCopy(SessionBridgeProtocol.Physics, buffer));
        Assert.Equal(3, opens);
    }

    [Fact]
    public void First_tick_sends_each_page_and_later_ticks_follow_the_packet_id()
    {
        var physics = new byte[AcSharedLayout.PhysicsSize];
        var graphics = new byte[AcSharedLayout.GraphicsSize];
        var page = new byte[AcSharedLayout.StaticSize];
        BinaryPrimitives.WriteInt32LittleEndian(physics, 1);
        BinaryPrimitives.WriteInt32LittleEndian(graphics, 1);
        page[20] = 4;
        var pages = new FakePages { Physics = physics, Graphics = graphics, Static = page };
        var sink = new RecordingSink();
        var pump = new SharedPagePump(pages, sink);

        Assert.Equal(SharedPageSchedule.IdleMs, pump.Tick(0));
        Assert.Equal(3, sink.Frames.Count);
        Assert.Equal(SessionBridgeProtocol.Physics, sink.Frames[0].Type);
        Assert.Equal(1, sink.Frames[0].PacketId);
        Assert.Equal(SessionBridgeProtocol.Static, sink.Frames[2].Type);

        Assert.Equal(SharedPageSchedule.IdleMs - 10, pump.Tick(10));
        Assert.Equal(3, sink.Frames.Count);

        BinaryPrimitives.WriteInt32LittleEndian(physics, 4);
        Assert.Equal(SharedPageSchedule.LivePhysicsMs - 10, pump.Tick(10));
        Assert.Equal(3, sink.Frames.Count);
        pump.Tick(SharedPageSchedule.LivePhysicsMs);
        Assert.Equal(4, sink.Frames[^1].PacketId);
        Assert.Equal(SessionBridgeProtocol.Physics, sink.Frames[^1].Type);

        page[20] = 5;
        pump.Tick(SharedPageSchedule.LivePhysicsMs);
        Assert.Equal(SessionBridgeProtocol.Static, sink.Frames[^1].Type);
        Assert.Equal(2, sink.Frames[^1].PacketId);
    }

    [Fact]
    public void Graphics_uses_its_own_interval_and_a_quiet_page_waits()
    {
        var graphics = new byte[AcSharedLayout.GraphicsSize];
        BinaryPrimitives.WriteInt32LittleEndian(graphics, 3);
        var pages = new FakePages { Graphics = graphics };
        var sink = new RecordingSink();
        var pump = new SharedPagePump(pages, sink);

        Assert.Equal(SharedPageSchedule.IdleMs, pump.Tick(0));
        BinaryPrimitives.WriteInt32LittleEndian(graphics, 8);
        pump.Tick(SharedPageSchedule.LiveGraphicsMs);
        Assert.Equal(8, sink.Frames[^1].PacketId);
        Assert.Equal(SessionBridgeProtocol.Graphics, sink.Frames[^1].Type);

        var quiet = new SharedPagePump(new FakePages(), new RecordingSink());
        Assert.Equal(SharedPageSchedule.DisconnectedMs, quiet.Tick(0));
    }

    [Fact]
    public void Loop_stops_after_the_game_leaves_and_when_it_never_starts()
    {
        var playing = true;
        var ticks = 0;
        BridgeLoop.Run(
            new SharedPagePump(new FakePages(), new RecordingSink()),
            () => playing,
            () => ticks,
            _ => {
                ticks++;
                if (ticks == 2) {
                    playing = false;
                }
            });
        Assert.Equal(2, ticks);

        long now = 0;
        var waited = 0;
        BridgeLoop.Run(
            new SharedPagePump(new FakePages(), new RecordingSink()),
            () => false,
            () => now,
            delay => {
                waited++;
                now += delay;
            });
        Assert.True(now >= BridgeLoop.GameStartGraceMs);
        Assert.True(waited > 0);
    }

    private sealed class FakePages : ISharedPages
    {
        public byte[]? Physics { get; set; }

        public byte[]? Graphics { get; set; }

        public byte[]? Static { get; set; }

        public bool TryCopy(byte kind, Span<byte> destination)
        {
            var source = kind switch {
                SessionBridgeProtocol.Physics => Physics,
                SessionBridgeProtocol.Graphics => Graphics,
                SessionBridgeProtocol.Static => Static,
                _ => null,
            };
            if (source == null || source.Length != destination.Length) {
                return false;
            }

            source.CopyTo(destination);
            return true;
        }
    }

    private sealed class BytesPage : IMappedPage
    {
        private readonly byte[] _bytes;

        public BytesPage(byte[] bytes) => _bytes = bytes;

        public bool TryCopy(Span<byte> destination)
        {
            if (_bytes.Length != destination.Length) {
                return false;
            }

            _bytes.CopyTo(destination);
            return true;
        }

        public void Dispose()
        {
        }
    }

    private sealed record Frame(byte Type, int PacketId);

    private sealed class RecordingSink : ISessionFrameSink
    {
        public List<Frame> Frames { get; } = [];

        public void Publish(byte type, int packetId, ReadOnlySpan<byte> payload) =>
            Frames.Add(new Frame(type, packetId));
    }
}
