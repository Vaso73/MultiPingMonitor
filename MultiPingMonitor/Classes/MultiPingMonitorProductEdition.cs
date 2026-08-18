namespace MultiPingMonitor.Classes
{
    /// <summary>
    /// Central product boundary shared by UI, commands and update workflows.
    /// Public Free is built from the current codebase but intentionally exposes
    /// only the Normal monitoring workflow.
    /// </summary>
    public static class MultiPingMonitorProductEdition
    {
#if MULTIPINGMONITOR_FREE
        public static bool IsPublicFree { get; } = true;
        public static bool IsSponsorPro { get; } = false;
        public static bool SupportsCompactMode { get; } = false;
        public static bool SupportsCompactSets { get; } = false;
        public static bool SupportsLivePing { get; } = false;
        public static bool SupportsNetworkIdentity { get; } = false;
        public static bool SupportsExternalLanguagePacks { get; } = true;
        public static bool SupportsSponsorProUpdates { get; } = false;
        public static bool SupportsSponsorProUpgrade { get; } = true;
        public static string EditionName { get; } = "Public Free";
#else
        public static bool IsPublicFree { get; } = false;
        public static bool IsSponsorPro { get; } = true;
        public static bool SupportsCompactMode { get; } = true;
        public static bool SupportsCompactSets { get; } = true;
        public static bool SupportsLivePing { get; } = true;
        public static bool SupportsNetworkIdentity { get; } = true;
        public static bool SupportsExternalLanguagePacks { get; } = true;
        public static bool SupportsSponsorProUpdates { get; } = true;
        public static bool SupportsSponsorProUpgrade { get; } = false;
        public static string EditionName { get; } = "Sponsor Pro";
#endif
    }
}
