using System;
using MultiPingMonitor.Properties;

namespace MultiPingMonitor.Classes
{
    public enum StatusChangeEventType
    {
        Probe,
        NetworkIdentity
    }

    public class StatusChangeLog
    {
        public DateTime Timestamp { get; set; }
        public string Hostname { get; set; }
        public string Alias { get; set; }
        public ProbeStatus Status { get; set; }
        public bool HasStatusBeenCleared { get; set; }
        public StatusChangeEventType EventType { get; set; } = StatusChangeEventType.Probe;

        public bool IsNetworkIdentityEvent =>
            EventType == StatusChangeEventType.NetworkIdentity;

        public string EventTypeAsString =>
            EventType == StatusChangeEventType.NetworkIdentity
                ? "Network identity"
                : "Probe status";

        /// <summary>
        /// Optional display text for non-probe entries such as LAN/WAN IP changes.
        /// When empty, the normal ProbeStatus localized text is used.
        /// </summary>
        public string CustomStatusText { get; set; }

        /// <summary>
        /// Optional Marlett glyph override for non-probe entries.
        /// </summary>
        public string CustomGlyph { get; set; }


        public string PopupTitle { get; set; }
        public string PopupDetailPrimary { get; set; }
        public string PopupDetailSecondary { get; set; }

        public bool HasPopupDetail =>
            !string.IsNullOrWhiteSpace(PopupDetailPrimary)
            || !string.IsNullOrWhiteSpace(PopupDetailSecondary);

        public string PopupTitleOrAddress =>
            !string.IsNullOrWhiteSpace(PopupTitle)
                ? PopupTitle
                : AliasIfExistOrHostname;

        public string PopupStatusText =>
            HasPopupDetail
                ? PopupDetailPrimary
                : StatusAsString;

        public string PopupSecondaryText =>
            HasPopupDetail
                ? PopupDetailSecondary
                : string.Empty;public string AliasIfExistOrHostname =>
            !string.IsNullOrWhiteSpace(Alias) ? Alias : Hostname;

        public string StatusAsString
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CustomStatusText))
                    return CustomStatusText;

                switch (Status)
                {
                    case ProbeStatus.Down:
                        return Strings.StatusChange_Down;
                    case ProbeStatus.Up:
                        return Strings.StatusChange_Up;
                    case ProbeStatus.Error:
                        return Strings.StatusChange_Error;
                    case ProbeStatus.Start:
                        return Strings.StatusChange_Start;
                    case ProbeStatus.Stop:
                        return Strings.StatusChange_Stop;
                    case ProbeStatus.LatencyHigh:
                        return Strings.StatusChange_LatencyHigh;
                    case ProbeStatus.LatencyNormal:
                        return Strings.StatusChange_LatencyNormal;
                    default:
                        return string.Empty;
                }
            }
        }

        public string StatusAsGlyph
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CustomGlyph))
                    return CustomGlyph;

                switch (Status)
                {
                    case ProbeStatus.Error:
                    case ProbeStatus.Down:
                        return "u";
                    case ProbeStatus.Up:
                        return "t";
                    default:
                        return "h";
                }
            }
        }
    }
}
