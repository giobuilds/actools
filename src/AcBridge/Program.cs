using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Runtime.Versioning;
using AcManager.Core.Bridge;

[assembly: SupportedOSPlatform("windows")]

var port = BridgeArguments.Port(args);
try {
    Run(port);
} catch (SocketException) {
    Console.Error.WriteLine(
        "AcBridge could not listen on 127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + ".");
    return 1;
}

return 0;

static void Run(int port)
{
    using var pages = new WindowsSharedPages();
    using var host = new SessionBridgeHost(port: port);
    var pump = new SharedPagePump(pages, host);
    var clock = Stopwatch.StartNew();
    BridgeLoop.Run(pump, AcsIsRunning, () => clock.ElapsedMilliseconds, Thread.Sleep);
}

static bool AcsIsRunning()
{
    Process[] processes;
    try {
        processes = Process.GetProcesses();
    } catch (SystemException) {
        return false;
    }

    try {
        foreach (var process in processes) {
            try {
                var name = process.ProcessName;
                if (name.Equals("acs", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("acs.exe", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("acs_pro", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("acs_pro.exe", StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            } catch (InvalidOperationException) {
                // The process exited while its name was being read.
            }
        }
    } finally {
        foreach (var process in processes) {
            process.Dispose();
        }
    }

    return false;
}
