using AcTools.Core.Sessions;
using AcTools.Platform;

namespace AcManager.Core.Sessions;

public static class PracticeLaunch
{
    public static string WriteRaceIni(AcInstallLayout layout, PracticeSession session)
    {
        if (string.IsNullOrWhiteSpace(layout.DocumentsDirectory)) {
            throw new InvalidOperationException("The Assetto Corsa documents directory is not set. Open Install and choose the Proton prefix Documents folder.");
        }

        return RaceFiles.WriteRaceIni(layout.DocumentsDirectory, PracticeRaceIni.Build(session));
    }
}
