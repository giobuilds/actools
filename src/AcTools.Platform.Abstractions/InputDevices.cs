using System.Globalization;

namespace AcTools.Platform;

/// <summary>
/// A joystick SDL2 can see. The name is the SDL name, which often differs from the DirectInput name in controls.ini.
/// </summary>
public sealed record InputDevice(string Name, string Guid, int? VendorId, int? ProductId)
{
    public string Label =>
        VendorId is int vendor && ProductId is int product
            ? Name + "  " + vendor.ToString("X4", CultureInfo.InvariantCulture) + ":" + product.ToString("X4", CultureInfo.InvariantCulture)
            : Name;
}

public sealed record InputDeviceList(IReadOnlyList<InputDevice> Devices, string? Unavailable);

public interface IInputDevices
{
    InputDeviceList List();
}
