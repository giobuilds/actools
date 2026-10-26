#nullable disable
using System.Runtime.InteropServices;

namespace AcManager.Core.Bridge;

/// <summary>
/// Plain sequential fields for the three Assetto Corsa shared-memory pages.
/// Bools are 4-byte Windows BOOL values. Text is UTF-16. A Vector3 is three floats.
/// </summary>
public static class AcSharedLayout
{
    public static readonly int PhysicsSize = Marshal.SizeOf<AcSharedPhysicsPage>();

    public static readonly int GraphicsSize = Marshal.SizeOf<AcSharedGraphicsPage>();

    public static readonly int StaticSize = Marshal.SizeOf<AcSharedStaticPage>();

    public const int GearOffset = 16;

    public const int SpeedOffset = 28;

    public const int CompletedLapsOffset = 132;

    public const int PositionOffset = 136;

    public const int CarModelOffset = 68;

    public const int TrackOffset = 134;

    public const int IdCharacters = 33;

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Vec3
    {
        public float X;
        public float Y;
        public float Z;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
public struct AcSharedPhysicsPage
{
    public int PacketId;
    public float Gas;
    public float Brake;
    public float Fuel;
    public int Gear;
    public int Rpms;
    public float SteerAngle;
    public float SpeedKmh;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] Velocity;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] AccG;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelSlip;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelLoad;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelPressure;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelAngularSpeed;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreWear;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreDirtyLevel;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreCoreTemperature;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] CamberRad;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] SuspensionTravel;

    public float Drs;
    public float Tc;
    public float Heading;
    public float Pitch;
    public float Roll;
    public float CgHeight;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
    public float[] CarDamage;

    public int NumberOfTyresOut;

    [MarshalAs(UnmanagedType.Bool)]
    public bool PitLimiterOn;

    public float Abs;
    public float KersCharge;
    public float KersInput;

    [MarshalAs(UnmanagedType.Bool)]
    public bool AutoShifterOn;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public float[] RideHeight;

    public float TurboBoost;
    public float Ballast;
    public float AirDensity;
    public float AirTemp;
    public float RoadTemp;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] LocalAngularVelocity;

    public float FinalFf;
    public float PerformanceMeter;
    public int EngineBrake;
    public int ErsRecoveryLevel;
    public int ErsPowerLevel;
    public int ErsHeatCharging;
    public int ErsIsCharging;
    public float KersCurrentKj;

    [MarshalAs(UnmanagedType.Bool)]
    public bool DrsAvailable;

    [MarshalAs(UnmanagedType.Bool)]
    public bool DrsEnabled;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] BrakeTemperature;

    public float Clutch;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTempI;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTempM;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTempO;

    [MarshalAs(UnmanagedType.Bool)]
    public bool IsAiControlled;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public AcSharedLayout.Vec3[] TyreContactPoint;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public AcSharedLayout.Vec3[] TyreContactNormal;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public AcSharedLayout.Vec3[] TyreContactHeading;

    public float BrakeBias;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] LocalVelocity;
}

[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
public struct AcSharedGraphicsPage
{
    public int PacketId;
    public int Status;
    public int Session;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string CurrentTime;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string LastTime;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string BestTime;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string Split;

    public int CompletedLaps;
    public int Position;
    public int CurrentTimeMs;
    public int LastTimeMs;
    public int BestTimeMs;
    public float SessionTimeLeft;
    public float DistanceTraveled;

    [MarshalAs(UnmanagedType.Bool)]
    public bool IsInPits;

    public int CurrentSectorIndex;
    public int LastSectorTime;
    public int NumberOfLaps;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string TyreCompound;

    public float ReplayTimeMultiplier;
    public float NormalizedCarPosition;
    public AcSharedLayout.Vec3 CarCoordinates;
    public float PenaltyTime;
    public int Flag;

    [MarshalAs(UnmanagedType.Bool)]
    public bool IdealLineOn;

    [MarshalAs(UnmanagedType.Bool)]
    public bool IsInPitLane;

    public float SurfaceGrip;

    [MarshalAs(UnmanagedType.Bool)]
    public bool MandatoryPitDone;
}

[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
public struct AcSharedStaticPage
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string SharedMemoryVersion;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string AssettoCorsaVersion;

    public int NumberOfSessions;
    public int NumberOfCars;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string CarModel;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string Track;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string PlayerName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string PlayerSurname;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string PlayerNickname;

    public int SectorCount;
    public float MaxTorque;
    public float MaxPower;
    public int MaxRpm;
    public float MaxFuel;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] SuspensionMaxTravel;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreRadius;

    public float MaxTurboBoost;
    public float AirTemperature;
    public float RoadTemperature;
    public int PenaltiesEnabled;
    public float AidFuelRate;
    public float AidTireRate;
    public float AidMechanicalDamage;
    public int AidAllowTyreBlankets;
    public float AidStability;
    public int AidAutoClutch;
    public int AidAutoBlip;

    [MarshalAs(UnmanagedType.Bool)]
    public bool HasDrs;

    [MarshalAs(UnmanagedType.Bool)]
    public bool HasErs;

    [MarshalAs(UnmanagedType.Bool)]
    public bool HasKers;

    public float KersMaxJoules;
    public int EngineBrakeSettingsCount;
    public int ErsPowerControllerCount;
    public float TrackSplineLength;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string TrackConfiguration;

    public float ErsMaxJ;

    [MarshalAs(UnmanagedType.Bool)]
    public bool IsTimedRace;

    [MarshalAs(UnmanagedType.Bool)]
    public bool HasExtraLap;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string CarSkin;

    public int ReversedGridPositions;
    public int PitWindowStart;
    public int PitWindowEnd;
}
