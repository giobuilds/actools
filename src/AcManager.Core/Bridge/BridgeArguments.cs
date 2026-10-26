using System.Globalization;

namespace AcManager.Core.Bridge;

public static class BridgeArguments
{
    public static int Port(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count - 1; i++) {
            if (!string.Equals(args[i], "--port", StringComparison.Ordinal)) {
                continue;
            }

            if (int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var port)
                && port is >= 1 and <= 65535) {
                return port;
            }
        }

        return SessionBridgeProtocol.DefaultPort;
    }
}
