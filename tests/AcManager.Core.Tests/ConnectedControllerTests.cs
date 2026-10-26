using System.Globalization;
using AcManager.Core.Settings;
using AcTools.Platform;
using AcTools.Platform.Linux;
using Xunit;

namespace AcManager.Core.Tests;

public class ConnectedControllerTests
{
    [Fact]
    public void TryUsb_reads_ids_when_the_guid_has_a_name_checksum()
    {
        Assert.True(SdlGuid.TryUsb(Parse("030000005e0400008e02000000000000"), out var vendor, out var product));
        Assert.Equal(0x045e, vendor);
        Assert.Equal(0x028e, product);
        Assert.Equal("030000005e0400008e02000000000000", SdlGuid.Hex(Parse("030000005e0400008e02000000000000")));

        Assert.True(SdlGuid.TryUsb(Parse("030065a94f0400000ab1000000010000"), out vendor, out product));
        Assert.Equal(0x044f, vendor);
        Assert.Equal(0xb10a, product);

        Assert.False(SdlGuid.TryUsb(Parse("ff0000005e0400008e02000000000000"), out _, out _));
        Assert.False(SdlGuid.TryUsb(Parse("00000000000000000000000000000000"), out _, out _));
        Assert.False(SdlGuid.TryUsb(Parse("030000005468727573746d6173746572"), out _, out _));
    }

    [Fact]
    public void Lines_skip_the_bind_note_when_every_name_matches()
    {
        var connected = new InputDeviceList([
            new InputDevice("Logitech G29 Driving Force Racing Wheel", "aa", null, null),
        ], null);

        var lines = ConnectedControllers.Lines(["  logitech g29 driving force racing wheel "], connected);

        Assert.Equal(["Logitech G29 Driving Force Racing Wheel"], lines);
    }

    [Fact]
    public void Lines_add_the_bind_note_when_a_name_differs()
    {
        var connected = new InputDeviceList([
            new InputDevice("Logitech G920 Driving Force Racing Wheel", "aa", 0x046d, 0xc262),
        ], null);

        var lines = ConnectedControllers.Lines(["G920"], connected);

        Assert.Equal([
            "Logitech G920 Driving Force Racing Wheel  046D:C262",
            ConnectedControllers.BindOnce,
        ], lines);
    }

    [Fact]
    public void Lines_pass_through_an_unavailable_list()
    {
        var lines = ConnectedControllers.Lines(
            ["G29"],
            new InputDeviceList([], "SDL2 is not installed, so connected devices are not listed."));

        Assert.Equal(["SDL2 is not installed, so connected devices are not listed."], lines);
    }

    [Fact]
    public void Lines_say_when_nothing_is_connected()
    {
        var lines = ConnectedControllers.Lines([], new InputDeviceList([], null));

        Assert.Equal(["No joysticks are connected."], lines);
    }

    [Fact]
    public void List_names_joysticks_when_sdl_is_installed()
    {
        var list = new SdlInputDevices().List();

        Assert.Null(list.Unavailable);
        foreach (var device in list.Devices) {
            Assert.False(string.IsNullOrWhiteSpace(device.Name));
            Assert.Matches("^[0-9a-f]{32}$", device.Guid);
        }
    }

    private static byte[] Parse(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++) {
            bytes[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return bytes;
    }
}
