namespace Hydra.Vitals.Core
{
    public enum VitalType
    {
        ANR = 1,
        Crash = 2,
        MemoryLeak = 3,
        ColdStart = 4,
        HotStart = 5,
        Jank = 6,
        LogcatObservation = 7
    }

    public enum VitalSeverity
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    public enum VitalStatus
    {
        Open = 1,
        PartiallyMitigated = 2,
        Mitigated = 3,
        FixedAwaitingRelease = 4,
        FixedVerified = 5,
        ClosedNotActionable = 6,
        ClosedOsDriverBug = 7,
        FrameworkMonitored = 8,
        Duplicate = 9
    }

    public enum ProjectPlatformType
    {
        MobileAndroid = 1,
        MobileIos = 2,
        DesktopWindows = 3,
        DesktopMac = 4,
        DesktopLinux = 5,
        SharedLibrary = 6,
        WebApi = 7,
        WebApp = 8
    }
}
