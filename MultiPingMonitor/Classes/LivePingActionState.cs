using System;

namespace MultiPingMonitor.Classes
{
    internal readonly struct LivePingActionState
    {
        private LivePingActionState(
            bool canStart,
            bool canCopyTarget,
            bool canCopyAddress,
            bool canPauseResume,
            bool canClear)
        {
            CanStart = canStart;
            CanCopyTarget = canCopyTarget;
            CanCopyAddress = canCopyAddress;
            CanPauseResume = canPauseResume;
            CanClear = canClear;
        }

        internal bool CanStart { get; }

        internal bool CanCopyTarget { get; }

        internal bool CanCopyAddress { get; }

        internal bool CanPauseResume { get; }

        internal bool CanClear { get; }

        internal static LivePingActionState Evaluate(
            bool isManualMode,
            string manualInput,
            string target,
            string resolvedAddress,
            int logLineCount,
            uint sent,
            uint received,
            uint lost)
        {
            bool hasManualInput =
                !string.IsNullOrWhiteSpace(manualInput);
            bool hasTarget =
                !string.IsNullOrWhiteSpace(target);
            bool hasResolvedAddress =
                !string.IsNullOrWhiteSpace(resolvedAddress);
            bool hasResults =
                logLineCount > 0
                || sent > 0
                || received > 0
                || lost > 0;

            return new LivePingActionState(
                isManualMode && hasManualInput,
                hasTarget,
                hasResolvedAddress,
                hasTarget,
                hasResults);
        }
    }
}
