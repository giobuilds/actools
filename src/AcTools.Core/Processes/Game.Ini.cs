#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.DataFile;

namespace AcTools.Processes {
    public partial class Game {
        public abstract class RaceIniProperties {
            public abstract void Set(IniFile file);
        }

        /// <summary>
        /// The Windows writer used My Documents. Callers pass the prefix Documents directory.
        /// </summary>
        public abstract class AdditionalProperties {
            public abstract IDisposable Set(string documentsDirectory);
        }

        public static void ClearUpIniFile(IniFile file) {
            file["BENCHMARK"].Set("ACTIVE", false);
            file["REPLAY"].Set("ACTIVE", false);
            file["REMOTE"].Set("ACTIVE", false);
            file["REMOTE"].Remove("__FEATURES");
            file["REMOTE"].Remove("__CM_EXTENDED");
            file["RESTART"].Set("ACTIVE", false);
            file["__PREVIEW_GENERATION"].Set("ACTIVE", false);
            file["LIGHTING"].Remove("__CM_WEATHER_TYPE");
            file["LIGHTING"].Remove("__CM_WEATHER_CONTROLLER");
            file["LIGHTING"].Remove("__CM_DATE");
            file["LIGHTING"].Remove("__CM_DATE_USE_TIME");
            file["LIGHTING"].Remove("__CM_WEATHER_HUMIDITY");
            file["LIGHTING"].Remove("__CM_WEATHER_PRESSURE");
            file["RACE"].Remove("__CM_CUSTOM_MODE");
            file["OPTIONS"].Remove("__BACKGROUND_IMAGE");

            file.RemoveSections("CAR", 1);
            file.RemoveSections("SESSION");
            file.RemoveSections("CONDITION");

            file.Remove("EVENT");
            file.Remove("SPECIAL_EVENT");
        }

        private static void SetDefaultProperties(IniFile file) {
            file["HEADER"].Set("VERSION", 2);
            file["LAP_INVALIDATOR"].Set("ALLOWED_TYRES_OUT", -1);
        }

        public class StartProperties {
            public IniFile PreparedConfig;
            public BasicProperties BasicProperties;
            public AssistsProperties AssistsProperties;
            public ConditionProperties ConditionProperties;
            public TrackProperties TrackProperties;
            public BaseModeProperties ModeProperties;
            public ReplayProperties ReplayProperties;
            public BenchmarkProperties BenchmarkProperties;
            public List<object> AdditionalPropertieses = new List<object>();

            public StartProperties() { }

            public StartProperties(BenchmarkProperties benchmarkProperties) {
                BenchmarkProperties = benchmarkProperties;
            }

            public StartProperties(ReplayProperties replayProperties) {
                ReplayProperties = replayProperties;
            }

            public StartProperties(BasicProperties basicProperties, AssistsProperties assistsProperties, ConditionProperties conditionProperties,
                    TrackProperties trackProperties, BaseModeProperties modeProperties) {
                BasicProperties = basicProperties;
                AssistsProperties = assistsProperties;
                ConditionProperties = conditionProperties;
                TrackProperties = trackProperties;
                ModeProperties = modeProperties;
            }

            /// <summary>
            /// Builds race.ini in memory. It does not write the file or start the game.
            /// </summary>
            public IniFile BuildRaceIni() {
                var iniFile = PreparedConfig;
                if (iniFile == null) {
                    iniFile = new IniFile();
                    ClearUpIniFile(iniFile);
                    SetDefaultProperties(iniFile);

                    if (BasicProperties != null) {
                        BasicProperties.Set(iniFile);
                        ModeProperties?.Set(iniFile);
                        ConditionProperties?.Set(iniFile);
                        TrackProperties?.Set(iniFile);
                    } else if (ReplayProperties != null) {
                        ReplayProperties.Set(iniFile);
                    } else if (BenchmarkProperties != null) {
                        BenchmarkProperties.Set(iniFile);
                    } else {
                        throw new NotSupportedException();
                    }
                }

                foreach (var properties in AdditionalPropertieses.OfType<RaceIniProperties>()) {
                    properties.Set(iniFile);
                }

                return iniFile;
            }
        }
    }
}
