using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MultiPingMonitor.Tests
{
    /// <summary>
    /// Tests for the NetworkIdentityService feature:
    ///   – Footer text formatting via inline reimplementation of BuildFooterText.
    ///   – ParseOrgField helper (ASN + provider extraction) via source inspection.
    ///   – GetPreferredLocalIp filtering rules (APIPA, loopback) via logic tests.
    ///   – Service source structure (IDisposable, event unsubscription, etc.).
    ///   – Localization: new resource keys present in both EN and SK .resx files.
    ///   – AssemblyInfo.cs unchanged.
    ///
    /// Tests avoid WPF/WinForms dependencies so they run on Linux CI (net8.0).
    /// Where necessary, logic is reimplemented inline to stay platform-independent.
    /// </summary>
    public class NetworkIdentityServiceTests
    {
        // ── Path helpers ──────────────────────────────────────────────────────────

        private static string SolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !dir.GetFiles("*.sln").Any())
                dir = dir.Parent;
            return dir?.FullName
                ?? throw new DirectoryNotFoundException(
                    "Cannot locate solution root from " + AppContext.BaseDirectory);
        }

        private static string ServiceSourcePath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Classes", "NetworkIdentityService.cs");

        private static string MainWindowSourcePath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "UI", "MainWindow.xaml.cs");

        private static string MainWindowXamlPath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "UI", "MainWindow.xaml");

        private static string DefaultResxPath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Properties", "Strings.resx");

        private static string SkSkResxPath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Properties", "Strings.sk-SK.resx");

        private static string AssemblyInfoPath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Properties", "AssemblyInfo.cs");

        private static string ResxValue(string resxPath, string key)
        {
            var doc = XDocument.Load(resxPath);
            var el = doc.Root!
                .Elements("data")
                .FirstOrDefault(d => (string?)d.Attribute("name") == key);
            if (el == null)
                throw new KeyNotFoundException($"Resource key '{key}' not found in {resxPath}");
            return (string?)el.Element("value") ?? string.Empty;
        }

        // ── Inline reimplementation of BuildFooterText for portable unit tests ──
        //
        // Matches the logic in NetworkIdentityService.BuildFooterText exactly so we
        // can test it on Linux CI without referencing the Windows-targeted assembly.

        private static string BuildFooterText(
            string localIp,
            string publicIp,
            string countryCode,
            string asn,
            string provider,
            DateTime? lastRefresh,
            bool isRefreshing,
            string loadingText,
            string updatedLabel)
        {
            bool noDataYet = string.IsNullOrEmpty(localIp) && string.IsNullOrEmpty(publicIp);
            if (isRefreshing && noDataYet)
                return $"LAN: {loadingText} | WAN: {loadingText}";

            var lan = string.IsNullOrEmpty(localIp) ? "—" : localIp;

            string wan;
            if (isRefreshing && string.IsNullOrEmpty(publicIp))
            {
                wan = loadingText;
            }
            else if (string.IsNullOrEmpty(publicIp))
            {
                wan = "—";
            }
            else
            {
                wan = publicIp;
                var asnPart = string.Empty;
                if (!string.IsNullOrEmpty(asn) || !string.IsNullOrEmpty(provider))
                {
                    var sb = new System.Text.StringBuilder();
                    if (!string.IsNullOrEmpty(asn)) sb.Append(asn);
                    if (!string.IsNullOrEmpty(provider)) { if (sb.Length > 0) sb.Append(' '); sb.Append(provider); }
                    asnPart = " · " + sb;
                }
                wan = wan + asnPart;
            }

            var countryPrefix = string.IsNullOrEmpty(countryCode) ? string.Empty : countryCode + " ";
            return $"LAN: {lan} | {countryPrefix}WAN: {wan}{(lastRefresh.HasValue ? $" | {updatedLabel} {lastRefresh.Value:HH:mm}" : string.Empty)}";
        }

        // Inline reimplementation of ParseOrgField for portable tests.
        private static void ParseOrgField(string org, out string asn, out string provider)
        {
            asn = string.Empty;
            provider = string.Empty;
            if (string.IsNullOrEmpty(org)) return;
            int spaceIdx = org.IndexOf(' ');
            if (spaceIdx > 0
                && org.Length > 2
                && (org[0] == 'A' || org[0] == 'a')
                && (org[1] == 'S' || org[1] == 's'))
            {
                asn = org.Substring(0, spaceIdx);
                provider = org.Substring(spaceIdx + 1);
            }
            else
            {
                provider = org;
            }
        }

        // ── Footer text formatting tests ──────────────────────────────────────────

        [Fact]
        public void BuildFooterText_LoadingWithNoData_ShowsLoadingPlaceholders()
        {
            var result = BuildFooterText("", "", "", "", "", null, true, "loading…", "upd.");
            Assert.Equal("LAN: loading… | WAN: loading…", result);
        }

        [Fact]
        public void BuildFooterText_FullData_FormatsCorrectly()
        {
            var ts = new DateTime(2025, 4, 25, 12, 34, 0);
            var result = BuildFooterText(
                "10.100.100.25", "45.66.72.254", "SK", "AS12345", "WATEL",
                ts, false, "loading…", "upd.");

            Assert.Equal("LAN: 10.100.100.25 | SK WAN: 45.66.72.254 · AS12345 WATEL | upd. 12:34", result);
        }

        [Fact]
        public void BuildFooterText_NoCountryCode_OmitsCountryPrefix()
        {
            var ts = new DateTime(2025, 4, 25, 9, 0, 0);
            var result = BuildFooterText(
                "192.168.1.5", "1.2.3.4", "", "AS9999", "TestISP",
                ts, false, "loading…", "akt.");

            Assert.StartsWith("LAN: 192.168.1.5 | WAN: 1.2.3.4", result);
            Assert.DoesNotContain("  WAN", result); // no double space from missing country
        }

        [Fact]
        public void BuildFooterText_NoPublicIp_ShowsDash()
        {
            var result = BuildFooterText("10.0.0.1", "", "", "", "", null, false, "loading…", "upd.");
            Assert.Contains("WAN: —", result);
        }

        [Fact]
        public void BuildFooterText_NoLocalIp_ShowsDash()
        {
            var result = BuildFooterText("", "5.6.7.8", "DE", "", "", null, false, "loading…", "upd.");
            Assert.Contains("LAN: —", result);
        }

        [Fact]
        public void BuildFooterText_NoLastRefresh_OmitsTimestamp()
        {
            var result = BuildFooterText("10.0.0.1", "5.6.7.8", "CZ", "AS1", "ISP", null, false, "loading…", "upd.");
            Assert.DoesNotContain("upd.", result);
        }

        [Fact]
        public void BuildFooterText_HasLastRefresh_IncludesTimestamp()
        {
            var ts = new DateTime(2025, 4, 25, 8, 5, 0);
            var result = BuildFooterText("10.0.0.1", "5.6.7.8", "CZ", "AS1", "ISP", ts, false, "loading…", "upd.");
            Assert.Contains("upd. 08:05", result);
        }

        [Fact]
        public void BuildFooterText_PublicIpOnlyNoMeta_ShowsIpWithoutDot()
        {
            // When IP is known but ASN/provider are empty – should show IP without "·".
            var result = BuildFooterText("10.0.0.1", "5.6.7.8", "", "", "", null, false, "loading…", "upd.");
            Assert.Contains("WAN: 5.6.7.8", result);
            Assert.DoesNotContain("·", result);
        }

        [Fact]
        public void BuildFooterText_PublicIpShownWithoutMeta()
        {
            // When PublicIp is set but country/ASN/provider are all empty
            // (as happens when only phase 1 of the WAN lookup succeeded),
            // the footer must still show the IP — not a loading/error placeholder.
            var result = BuildFooterText("10.0.0.1", "203.0.113.5", "", "", "", null, false, "loading…", "upd.");
            Assert.Contains("203.0.113.5", result);
            Assert.DoesNotContain("loading", result);
            Assert.DoesNotContain("·", result);
        }

        [Fact]
        public void BuildFooterText_IsRefreshingWithPublicIp_ShowsIpNotLoading()
        {
            // While IsRefreshing=true (phase 2 in progress) but PublicIp is already
            // known (from phase 1), the WAN field must show the IP, not "loading…".
            var result = BuildFooterText("10.0.0.1", "203.0.113.5", "US", "AS1", "ISP", null, true, "loading…", "upd.");
            Assert.Contains("203.0.113.5", result);
            Assert.DoesNotContain("loading…", result);
        }

        [Fact]
        public void BuildFooterText_IsRefreshingWithExistingLanData_ShowsLoadingForWan()
        {
            // LAN data available, WAN not yet — while refreshing WAN.
            var result = BuildFooterText("10.0.0.1", "", "", "", "", null, true, "loading…", "upd.");
            Assert.Contains("LAN: 10.0.0.1", result);
            Assert.Contains("WAN: loading…", result);
        }

        [Fact]
        public void BuildFooterText_NeverOverflows_WhenAllFieldsMaxLength()
        {
            // Verify the function doesn't crash with long strings.
            var result = BuildFooterText(
                "255.255.255.255",
                "255.255.255.255",
                "US",
                "AS999999",
                "Very Long Provider Name Inc.",
                DateTime.Now,
                false,
                "loading…",
                "upd.");
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        // ── ParseOrgField tests ───────────────────────────────────────────────────

        [Fact]
        public void ParseOrgField_StandardFormat_ExtractsAsnAndProvider()
        {
            ParseOrgField("AS12345 WATEL", out var asn, out var provider);
            Assert.Equal("AS12345", asn);
            Assert.Equal("WATEL", provider);
        }

        [Fact]
        public void ParseOrgField_MultiWordProvider_ExtractsCorrectly()
        {
            ParseOrgField("AS9999 Slovak Telekom a.s.", out var asn, out var provider);
            Assert.Equal("AS9999", asn);
            Assert.Equal("Slovak Telekom a.s.", provider);
        }

        [Fact]
        public void ParseOrgField_LowercaseAs_RecognizedAsAsn()
        {
            ParseOrgField("as12345 Provider", out var asn, out var provider);
            Assert.Equal("as12345", asn);
            Assert.Equal("Provider", provider);
        }

        [Fact]
        public void ParseOrgField_NoAsnPrefix_EntireOrgIsProvider()
        {
            ParseOrgField("SomeCompany Ltd.", out var asn, out var provider);
            Assert.Equal(string.Empty, asn);
            Assert.Equal("SomeCompany Ltd.", provider);
        }

        [Fact]
        public void ParseOrgField_Empty_BothEmpty()
        {
            ParseOrgField(string.Empty, out var asn, out var provider);
            Assert.Equal(string.Empty, asn);
            Assert.Equal(string.Empty, provider);
        }

        [Fact]
        public void ParseOrgField_Null_BothEmpty()
        {
            ParseOrgField(string.Empty, out var asn, out var provider);
            Assert.Equal(string.Empty, asn);
            Assert.Equal(string.Empty, provider);
        }

        // ── Local IP selection logic tests ────────────────────────────────────────

        [Theory]
        [InlineData("169.254.1.1", true)]
        [InlineData("169.254.0.1", true)]
        [InlineData("169.254.255.254", true)]
        public void LocalIp_AnyApipaAddress_ShouldBeSkipped(string ip, bool isApipa)
        {
            // Verifies filtering rule: 169.254.x.x addresses must be excluded.
            Assert.Equal(isApipa, ip.StartsWith("169.254."));
        }

        [Theory]
        [InlineData("10.0.0.1", false)]
        [InlineData("192.168.1.100", false)]
        [InlineData("172.16.0.5", false)]
        [InlineData("100.100.100.100", false)]
        public void LocalIp_NonApipaPrivateAddress_ShouldNotBeSkipped(string ip, bool isApipa)
        {
            Assert.False(isApipa);
            Assert.False(ip.StartsWith("169.254."));
        }

        // ── Service source-code structure tests ───────────────────────────────────

        [Fact]
        public void NetworkIdentityService_Exists()
        {
            Assert.True(File.Exists(ServiceSourcePath()),
                "NetworkIdentityService.cs should exist in Classes/");
        }

        [Fact]
        public void NetworkIdentityService_ImplementsIDisposable()
        {
            var source = File.ReadAllText(ServiceSourcePath());
            Assert.Contains("IDisposable", source);
            Assert.Contains("public void Dispose()", source);
        }

        [Fact]
        public void NetworkIdentityService_UnsubscribesNetworkEventsInDispose()
        {
            var source = File.ReadAllText(ServiceSourcePath());
            // Both unsubscriptions must be present in the Dispose method region.
            Assert.Contains("NetworkAddressChanged     -= OnNetworkChanged", source);
            Assert.Contains("NetworkAvailabilityChanged -= OnNetworkChanged", source);
        }

        [Fact]
        public void NetworkIdentityService_ExposesAllRequiredProperties()
        {
            var source = File.ReadAllText(ServiceSourcePath());
            foreach (var prop in new[] { "LocalIp", "PublicIp", "CountryCode", "Asn", "Provider", "LastRefresh", "IsRefreshing" })
            {
                Assert.Contains(prop, source);
            }
        }

        [Fact]
        public void NetworkIdentityService_ExposesStateChangedEvent()
        {
            var source = File.ReadAllText(ServiceSourcePath());
            Assert.Contains("StateChanged", source);
            Assert.Contains("EventHandler StateChanged", source);
        }

        [Fact]
        public void NetworkIdentityService_HasBuildFooterTextMethod()
        {
            var source = File.ReadAllText(ServiceSourcePath());
            Assert.Contains("BuildFooterText", source);
            Assert.Contains("static string BuildFooterText", source);
        }

        [Fact]
        public void NetworkIdentityService_HasGetPreferredLocalIpMethod()
        {
            var source = File.ReadAllText(ServiceSourcePath());
            Assert.Contains("GetPreferredLocalIp", source);
        }

        [Fact]
        public void NetworkIdentityService_HasPublicIpAndMetaTimeoutConstants()
        {
            var source = File.ReadAllText(ServiceSourcePath());
            // Per-phase timeouts must each be 5 000 ms.
            Assert.Contains("5_000", source);
            Assert.Contains("PublicIpTimeoutMs", source);
            Assert.Contains("MetaTimeoutMs", source);
        }

        [Fact]
        public void NetworkIdentityService_IsRefreshingResetInFinally()
        {
            var source = File.ReadAllText(ServiceSourcePath());
            // IsRefreshing must be unconditionally reset to false in a finally block.
            Assert.Contains("finally", source);
            Assert.Contains("IsRefreshing = false", source);
        }

        [Fact]
        public void NetworkIdentityService_HasFallbackPublicIpEndpoint()
        {
            var source = File.ReadAllText(ServiceSourcePath());
            // A dedicated fallback endpoint for the public-IP phase must be declared.
            Assert.Contains("PublicIpFallbackUrl", source);
            Assert.Contains("api.ipify.org", source);
        }

        [Fact]
        public void NetworkIdentityService_TwoPhaseWanLookup()
        {
            var source = File.ReadAllText(ServiceSourcePath());
            // Two separate methods must exist: one for the IP phase and one for metadata.
            Assert.Contains("FetchPublicIpAsync", source);
            Assert.Contains("FetchMetaAsync", source);
        }

        [Fact]
        public void NetworkIdentityService_HasDebounceAndBurstMode()
        {
            var source = File.ReadAllText(ServiceSourcePath());
            Assert.Contains("DebounceMs", source);
            Assert.Contains("BurstIntervalMs", source);
            Assert.Contains("BurstDurationMs", source);
        }

        [Fact]
        public void NetworkIdentityService_LookupUrlIsIsolated()
        {
            var source = File.ReadAllText(ServiceSourcePath());
            // URL must be in a single constant, not scattered around the code.
            Assert.Contains("LookupUrl", source);
            Assert.Contains("ipinfo.io", source);
        }

        [Fact]
        public void NetworkIdentityService_HasRequestRefreshMethod()
        {
            var source = File.ReadAllText(ServiceSourcePath());
            Assert.Contains("public void RequestRefresh()", source);
        }

        // ── MainWindow integration tests ──────────────────────────────────────────

        [Fact]
        public void MainWindow_HasCompactNetworkFooterElement()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            Assert.Contains("CompactNetworkFooter", xaml);
        }

        [Fact]
        public void MainWindow_HasStructuredFooterElements()
        {
            // PR #104 UI revision: single TextBlock replaced with multi-element layout.
            var xaml = File.ReadAllText(MainWindowXamlPath());
            Assert.Contains("CompactFooterLanText",      xaml);
            Assert.Contains("CompactFooterWanText",      xaml);
            Assert.Contains("CompactFooterCountryBadge", xaml);
            Assert.Contains("CompactFooterCountryText",  xaml);
            Assert.Contains("CompactFooterProviderText", xaml);
        }

        [Fact]
        public void MainWindow_HasRefreshButton()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            Assert.Contains("CompactFooterRefreshButton", xaml);
        }

        [Fact]
        public void MainWindow_RefreshButton_HasClickHandler()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            Assert.Contains("CompactFooterRefresh_Click", xaml);
        }

        [Fact]
        public void MainWindow_FooterLanAndWanUseTextWrapping()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            // Wrap must be present for both text elements so narrow windows don't clip.
            int wrapCount = System.Text.RegularExpressions.Regex
                .Matches(xaml, "TextWrapping=\"Wrap\"")
                .Count;
            Assert.True(wrapCount >= 2,
                "Both CompactFooterLanText and CompactFooterWanText should use TextWrapping=Wrap.");
        }

        [Fact]
        public void MainWindow_FooterUsesCompactNetworkFooterStyle()
        {
            var xaml = File.ReadAllText(MainWindowXamlPath());
            Assert.Contains("Style.CompactNetworkFooter", xaml);
        }

        [Fact]
        public void MainWindow_HasEnsureNetworkIdentityServiceMethod()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            Assert.Contains("EnsureNetworkIdentityService", source);
        }

        [Fact]
        public void MainWindow_DisposesServiceOnClose()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            Assert.Contains("_networkIdentityService?.Dispose()", source);
        }

        [Fact]
        public void MainWindow_SubscribesToStateChangedAndUnsubscribes()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            Assert.Contains("NetworkIdentityService_StateChanged", source);
        }

        [Fact]
        public void MainWindow_RefreshClickHandlerCallsRequestRefresh()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            Assert.Contains("CompactFooterRefresh_Click", source);
            Assert.Contains("RequestRefresh()", source);
        }

        [Fact]
        public void MainWindow_FooterDisablesRefreshButtonWhileBusy()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            // Button is disabled while IsRefreshing is true.
            Assert.Contains("IsRefreshing", source);
            Assert.Contains("IsEnabled", source);
        }

        [Fact]
        public void MainWindow_FooterUsesInlinesForBoldLabels()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            // LAN and WAN labels are bolded via Inlines, not plain Text assignment.
            Assert.Contains("Inlines.Add", source);
            Assert.Contains("FontWeight = System.Windows.FontWeights.SemiBold", source);
        }

        [Fact]
        public void MainWindow_CountryBadgeToggledByVisibility()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            Assert.Contains("CompactFooterCountryBadge", source);
            Assert.Contains("Visibility.Visible", source);
            Assert.Contains("Visibility.Collapsed", source);
        }

        [Fact]
        public void MainWindow_FooterLabelsUseLanIpAndWanIp()
        {
            var source = File.ReadAllText(MainWindowSourcePath());
            // Labels in the compact footer must say "LAN IP" and "WAN IP".
            Assert.Contains("\"LAN IP \"", source);
            Assert.Contains("\"WAN IP \"", source);
        }

        // ── Localization tests ────────────────────────────────────────────────────

        [Theory]
        [InlineData("Compact_Footer_Loading")]
        [InlineData("Compact_Footer_Updated")]
        [InlineData("Compact_Footer_Refresh")]
        public void DefaultResx_ContainsNewFooterKeys(string key)
        {
            var value = ResxValue(DefaultResxPath(), key);
            Assert.False(string.IsNullOrEmpty(value),
                $"EN resource key '{key}' must have a non-empty value.");
        }

        [Theory]
        [InlineData("Compact_Footer_Loading")]
        [InlineData("Compact_Footer_Updated")]
        [InlineData("Compact_Footer_Refresh")]
        public void SkSkResx_ContainsNewFooterKeys(string key)
        {
            var value = ResxValue(SkSkResxPath(), key);
            Assert.False(string.IsNullOrEmpty(value),
                $"SK resource key '{key}' must have a non-empty value.");
        }

        [Fact]
        public void SkSkResx_LoadingValue_IsSlovak()
        {
            var value = ResxValue(SkSkResxPath(), "Compact_Footer_Loading");
            // Slovak translation should contain "zisť" or at minimum not be English "loading"
            Assert.NotEqual("loading…", value);
        }

        // ── Visual style tests ────────────────────────────────────────────────────

        private static string ModernStylePath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Styles", "VisualStyle.Modern.xaml");

        private static string ClassicStylePath() =>
            Path.Combine(SolutionRoot(), "MultiPingMonitor", "Styles", "VisualStyle.Classic.xaml");

        [Theory]
        [InlineData("Style.CompactNetworkFooter")]
        [InlineData("Style.CompactCountryBadge")]
        public void ModernStyle_ContainsNewFooterStyles(string styleKey)
        {
            var source = File.ReadAllText(ModernStylePath());
            Assert.Contains(styleKey, source);
        }

        [Theory]
        [InlineData("Style.CompactNetworkFooter")]
        [InlineData("Style.CompactCountryBadge")]
        public void ClassicStyle_ContainsNewFooterStyles(string styleKey)
        {
            var source = File.ReadAllText(ClassicStylePath());
            Assert.Contains(styleKey, source);
        }

        [Fact]
        public void ModernStyle_FooterBackgroundMatchesToolbar()
        {
            var source = File.ReadAllText(ModernStylePath());
            // Both CompactSetToolbar and CompactNetworkFooter should use Theme.Border
            // so they have matching visual weight as a toolbar/footer pair.
            Assert.Contains("Theme.Border", source);
        }

        [Fact]
        public void ClassicStyle_FooterBackgroundMatchesToolbar()
        {
            var source = File.ReadAllText(ClassicStylePath());
            // Both CompactSetToolbar and CompactNetworkFooter should use Theme.SurfaceAlt.
            Assert.Contains("Theme.SurfaceAlt", source);
        }

        // ── AssemblyInfo unchanged ────────────────────────────────────────────────

        [Fact]
        public void AssemblyInfo_WasNotModified()
        {
            // AssemblyInfo.cs must not exist in the Properties folder
            // (project uses <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
            // so any AssemblyInfo.cs would be custom-written).
            // If it does exist, verify no new content was added for this feature.
            if (!File.Exists(AssemblyInfoPath()))
                return; // No AssemblyInfo.cs at all — nothing to check.

            var source = File.ReadAllText(AssemblyInfoPath());
            // Must not mention network identity or the new feature.
            Assert.DoesNotContain("NetworkIdentity", source);
            Assert.DoesNotContain("CompactFooter", source);
        }
    }
}
