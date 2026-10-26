using AcManager.Core.Packaging;
using Avalonia;

namespace AcManager.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--pack-appdir") {
            Environment.ExitCode = AppDirLayout.Run(args.Skip(1).ToArray());
            return;
        }

        if (Array.IndexOf(args, "--version") >= 0) {
            Console.WriteLine(AppVersion.Current);
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
