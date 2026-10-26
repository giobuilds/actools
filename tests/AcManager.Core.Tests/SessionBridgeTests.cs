using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using AcManager.Core.Bridge;
using Xunit;

namespace AcManager.Core.Tests;

public class SessionBridgeTests
{
    [Fact]
    public void Shared_memory_pages_keep_the_game_sizes_and_field_offsets()
    {
        Assert.Equal(580, AcSharedLayout.PhysicsSize);
        Assert.Equal(288, AcSharedLayout.GraphicsSize);
        Assert.Equal(648, AcSharedLayout.StaticSize);
        Assert.Equal(580, Marshal.SizeOf<AcSharedPhysicsPage>());
        Assert.Equal(288, Marshal.SizeOf<AcSharedGraphicsPage>());
        Assert.Equal(648, Marshal.SizeOf<AcSharedStaticPage>());
        Assert.Equal(AcSharedLayout.GearOffset, Marshal.OffsetOf<AcSharedPhysicsPage>(nameof(AcSharedPhysicsPage.Gear)).ToInt32());
        Assert.Equal(AcSharedLayout.SpeedOffset, Marshal.OffsetOf<AcSharedPhysicsPage>(nameof(AcSharedPhysicsPage.SpeedKmh)).ToInt32());
        Assert.Equal(AcSharedLayout.CompletedLapsOffset, Marshal.OffsetOf<AcSharedGraphicsPage>(nameof(AcSharedGraphicsPage.CompletedLaps)).ToInt32());
        Assert.Equal(AcSharedLayout.PositionOffset, Marshal.OffsetOf<AcSharedGraphicsPage>(nameof(AcSharedGraphicsPage.Position)).ToInt32());
        Assert.Equal(AcSharedLayout.CarModelOffset, Marshal.OffsetOf<AcSharedStaticPage>(nameof(AcSharedStaticPage.CarModel)).ToInt32());
        Assert.Equal(AcSharedLayout.TrackOffset, Marshal.OffsetOf<AcSharedStaticPage>(nameof(AcSharedStaticPage.Track)).ToInt32());
    }

    [Fact]
    public void Client_reads_a_live_frame_from_the_loopback_host()
    {
        using var host = new SessionBridgeHost();
        Assert.Equal(IPAddress.Loopback, host.Address);

        var physics = new byte[AcSharedLayout.PhysicsSize];
        BinaryPrimitives.WriteInt32LittleEndian(physics.AsSpan(AcSharedLayout.GearOffset), 3);
        BinaryPrimitives.WriteSingleLittleEndian(physics.AsSpan(AcSharedLayout.SpeedOffset), 142.5f);
        var graphics = new byte[AcSharedLayout.GraphicsSize];
        BinaryPrimitives.WriteInt32LittleEndian(graphics.AsSpan(AcSharedLayout.CompletedLapsOffset), 3);
        BinaryPrimitives.WriteInt32LittleEndian(graphics.AsSpan(AcSharedLayout.PositionOffset), 1);
        var page = new byte[AcSharedLayout.StaticSize];
        Encoding.Unicode.GetBytes("ks_ferrari_458\0").CopyTo(page, AcSharedLayout.CarModelOffset);
        Encoding.Unicode.GetBytes("ks_silverstone\0").CopyTo(page, AcSharedLayout.TrackOffset);
        host.Publish(SessionBridgeProtocol.Physics, 7, physics);
        host.Publish(SessionBridgeProtocol.Graphics, 7, graphics);
        host.Publish(SessionBridgeProtocol.Static, 1, page);

        var note = new SessionBridgeClient(host.Port, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)).ReadNote();

        Assert.Equal(
            "Live timing: ks_ferrari_458 on ks_silverstone, 3 laps completed, position 1, gear 2, 142.5 km/h.",
            note);
    }

    [Fact]
    public void Second_client_reads_a_frame_published_after_the_first_left()
    {
        using var host = new SessionBridgeHost();
        var first = new byte[AcSharedLayout.PhysicsSize];
        BinaryPrimitives.WriteInt32LittleEndian(first.AsSpan(AcSharedLayout.GearOffset), 2);
        BinaryPrimitives.WriteSingleLittleEndian(first.AsSpan(AcSharedLayout.SpeedOffset), 10f);
        host.Publish(SessionBridgeProtocol.Physics, 1, first);
        var earlier = new SessionBridgeClient(host.Port, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(200)).ReadNote();
        Assert.Contains("10 km/h", earlier, StringComparison.Ordinal);

        Thread.Sleep(100);
        var second = new byte[AcSharedLayout.PhysicsSize];
        BinaryPrimitives.WriteInt32LittleEndian(second.AsSpan(AcSharedLayout.GearOffset), 3);
        BinaryPrimitives.WriteSingleLittleEndian(second.AsSpan(AcSharedLayout.SpeedOffset), 142.5f);
        host.Publish(SessionBridgeProtocol.Physics, 2, second);
        var later = new SessionBridgeClient(host.Port, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(200)).ReadNote();

        Assert.Contains("gear 2", later, StringComparison.Ordinal);
        Assert.Contains("142.5 km/h", later, StringComparison.Ordinal);
    }

    [Fact]
    public void Client_refuses_a_hello_whose_page_size_does_not_match()
    {
        using var host = new SessionBridgeHost(physicsSize: 4);
        var note = new SessionBridgeClient(host.Port, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(200)).ReadNote();
        Assert.Equal(SessionBridgeProtocol.SizeNote, note);
    }

    [Fact]
    public void Client_says_live_timing_is_off_when_nothing_is_listening()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var note = new SessionBridgeClient(port, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(50)).ReadNote();

        Assert.Equal(SessionBridgeProtocol.OffNote, note);
    }
}
