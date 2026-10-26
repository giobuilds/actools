#nullable disable
using AcTools.DataFile;

namespace AcTools.Utils {
    public static class AcPaths {
        public static bool OptionEaseAcRootCheck;

        public static bool IsAcRoot(string directory) {
            return Directory.Exists(Path.Combine(directory, "content", "cars"))
                && Directory.Exists(Path.Combine(directory, "apps"))
                && (File.Exists(Path.Combine(directory, "acs.exe"))
                    || File.Exists(Path.Combine(directory, "acs_pro.exe")));
        }

        public static string GetSystemCfgDirectory(string acRoot) {
            return Path.Combine(acRoot, "system", "cfg");
        }

        public static string GetDocumentsCfgDirectory(string documentsDirectory) {
            return Path.Combine(documentsDirectory, "cfg");
        }

        public static string GetReplaysDirectory(string documentsDirectory) {
            return Path.Combine(documentsDirectory, "replay");
        }

        public static string GetDocumentsOutDirectory(string documentsDirectory) {
            return Path.Combine(documentsDirectory, "out");
        }

        public static string GetCfgShowroomFilename(string documentsDirectory) {
            return Path.Combine(GetDocumentsCfgDirectory(documentsDirectory), "showroom_start.ini");
        }

        public static string GetCfgVideoFilename(string documentsDirectory) {
            return Path.Combine(GetDocumentsCfgDirectory(documentsDirectory), "video.ini");
        }

        public static string GetCfgAppsFilename(string documentsDirectory) {
            return Path.Combine(GetDocumentsCfgDirectory(documentsDirectory), "python.ini");
        }

        public static string GetCfgControlsFilename(string documentsDirectory) {
            return Path.Combine(GetDocumentsCfgDirectory(documentsDirectory), "controls.ini");
        }

        public static string GetDocumentsScreensDirectory(string documentsDirectory) {
            return Path.Combine(documentsDirectory, "screens");
        }

        public static string GetCarsDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "cars");
        }

        public static string GetTracksDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "tracks");
        }

        public static string GetCarDirectory(string acRoot, string carName) {
            return Path.Combine(GetCarsDirectory(acRoot), carName);
        }

        public static string GetMainCarFilename(string carDir, bool considerHr) {
            return GetMainCarFilename(carDir, (IDataReadWrapper)null, considerHr);
        }

        /// <summary>
        /// data.acd stays encrypted. A loose data/lods.ini is the only lod list this reads.
        /// </summary>
        public static string GetMainCarFilename(string carDir, IDataReadWrapper data, bool considerHr) {
            var iniFile = data != null ? data.GetFile<IniFile>("lods.ini") : ReadLooseLods(carDir);
            if (iniFile != null && !iniFile.IsEmptyOrDamaged()) {
                var file = (considerHr ? iniFile["LOD_HR"].GetNonEmpty("FILE") : null)
                    ?? iniFile["LOD_0"].GetNonEmpty("FILE");
                if (file != null) {
                    return Path.Combine(carDir, file);
                }
            }

            if (!Directory.Exists(carDir)) {
                throw new DirectoryNotFoundException(carDir);
            }

            return LargestKn5(carDir);
        }

        public static string GetMainCarFilename(string acRoot, string carName, bool considerHr) {
            return GetMainCarFilename(GetCarDirectory(acRoot, carName), considerHr);
        }

        public static string GetCarSetupsDirectory(string documentsDirectory) {
            return Path.Combine(documentsDirectory, "setups");
        }

        public static string GetCarSetupsDirectory(string documentsDirectory, string carName) {
            return Path.Combine(documentsDirectory, "setups", carName);
        }

        public static string GetCarSkinsDirectory(string carDir) {
            return Path.Combine(carDir, "skins");
        }

        public static string GetCarSkinsDirectory(string acRoot, string carName) {
            return GetCarSkinsDirectory(GetCarDirectory(acRoot, carName));
        }

        public static string GetCarSkinDirectory(string acRoot, string carName, string skinName) {
            return Path.Combine(GetCarSkinsDirectory(acRoot, carName), skinName);
        }

        public static string GetShowroomsDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "showroom");
        }

        public static string GetFontsDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "fonts");
        }

        public static string GetWeatherDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "weather");
        }

        public static string GetPpFiltersDirectory(string acRoot) {
            return Path.Combine(acRoot, "system", "cfg", "ppfilters");
        }

        public static string GetDriverModelsDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "driver");
        }

        public static string GetPythonAppsDirectory(string acRoot) {
            return Path.Combine(acRoot, "apps", "python");
        }

        public static string GetKunosCareerDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "career");
        }

        public static string GetKunosCareerProgressFilename(string documentsDirectory) {
            return Path.Combine(documentsDirectory, "launcherdata", "filestore", "career.ini");
        }

        public static string GetShowroomDirectory(string acRoot, string showroomName) {
            return Path.Combine(GetShowroomsDirectory(acRoot), showroomName);
        }

        public static string GetAcLogoFilename(string acRoot) {
            return Path.Combine(acRoot, "content", "gui", "logo_ac_app.png");
        }

        public static string GetAcLauncherFilename(string acRoot) {
            return Path.Combine(acRoot, "AssettoCorsa.exe");
        }

        public static string GetLogFilename(string documentsDirectory) {
            return Path.Combine(documentsDirectory, "logs", "log.txt");
        }

        public static string GetLogFilename(string documentsDirectory, string logFileName) {
            return Path.Combine(documentsDirectory, "logs", logFileName);
        }

        public static string GetRaceIniFilename(string documentsDirectory) {
            return Path.Combine(GetDocumentsCfgDirectory(documentsDirectory), "race.ini");
        }

        public static string GetAssistsIniFilename(string documentsDirectory) {
            return Path.Combine(GetDocumentsCfgDirectory(documentsDirectory), "assists.ini");
        }

        public static string GetSfxDirectory(string acRoot) {
            return Path.Combine(acRoot, "content", "sfx");
        }

        public static string GetSfxGuidsFilename(string acRoot) {
            return Path.Combine(GetSfxDirectory(acRoot), "GUIDs.txt");
        }

        public static string GetGuiIconsFilename(string acRoot) {
            return Path.Combine(acRoot, "content", "gui", "icons");
        }

        public static string GetResultJsonFilename(string documentsDirectory) {
            return Path.Combine(GetDocumentsOutDirectory(documentsDirectory), "race_out.json");
        }

        private static IniFile ReadLooseLods(string carDir) {
            var loose = Path.Combine(carDir, "data", "lods.ini");
            if (!File.Exists(loose)) {
                return null;
            }

            return new IniFile(loose);
        }

        private static string LargestKn5(string carDir) {
            string best = null;
            long bestLength = -1;
            foreach (var path in Directory.GetFiles(carDir, "*.kn5")) {
                var length = new FileInfo(path).Length;
                if (best == null || length > bestLength) {
                    best = path;
                    bestLength = length;
                }
            }

            return best;
        }
    }
}
