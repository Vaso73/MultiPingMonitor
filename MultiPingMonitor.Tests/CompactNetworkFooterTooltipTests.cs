using System;
using System.IO;
using Xunit;

public class CompactNetworkFooterTooltipTests
{
    [Fact]
    public void MainWindow_CompactNetworkFooter_UsesDedicatedInfoButtonForSingleStablePopup()
    {
        string xaml = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml"));
        string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"CompactFooterInfoButton\"", xaml);
        Assert.Contains("Click=\"CompactFooterInfo_Click\"", xaml);
        Assert.Contains("x:Name=\"CompactFooterRefreshButton\"", xaml);
        Assert.Contains("Click=\"CompactFooterRefresh_Click\"", xaml);
        Assert.DoesNotContain("PreviewMouseLeftButtonUp=\"CompactNetworkFooter_MouseLeftButtonUp\"", xaml);

        Assert.Contains("private void CompactFooterInfo_Click", source);
        Assert.Contains("private System.Windows.Controls.Primitives.Popup _compactNetworkFooterInfoPopup;", source);
        Assert.Contains("_compactNetworkFooterInfoPopup.IsOpen = true;", source);
        Assert.Contains("TryFindResource(\"Theme.Accent\")", source);
        Assert.Contains("Height = 1", source);
        Assert.Contains("Opacity = 0.85", source);

        Assert.DoesNotContain("_compactNetworkFooterInfoMenu", source);
        Assert.DoesNotContain("_compactNetworkFooterClickToolTip", source);
        Assert.DoesNotContain("CompactNetworkFooter.ToolTip", source);
        Assert.DoesNotContain("CompactFooterLanText.ToolTip", source);
        Assert.DoesNotContain("CompactFooterWanText.ToolTip", source);
    }

    [Fact]
    public void MainWindow_CompactNetworkFooter_PopupContainsNetworkIdentityFields()
    {
        string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

        Assert.Contains("WAN IP:", source);
        Assert.Contains("NetworkIdentity_Title", source);
        Assert.Contains("NetworkIdentity_Country", source);
        Assert.Contains("Provider:", source);
        Assert.Contains("ASN:", source);
        Assert.Contains("LAN IP:", source);
        Assert.Contains("NetworkIdentity_LastWanCheck", source);
        Assert.Contains("NetworkIdentity_NextWanCheck", source);
        Assert.Contains("NetworkIdentity_LastWanState", source);
    }

    [Fact]
    public void NetworkIdentityService_TracksNextScheduledWanRefreshForPopup()
    {
        string source = File.ReadAllText(SourcePath("MultiPingMonitor", "Classes", "NetworkIdentityService.cs"));

        Assert.Contains("public DateTime? NextScheduledWanRefresh { get; private set; }", source);
        Assert.Contains("NextScheduledWanRefresh = DateTime.UtcNow.AddMilliseconds(WanPollIntervalMs);", source);
    }

    [Fact]
    public void MainWindow_CompactNetworkFooter_PopupUsesExactNextScheduledWanRefresh()
    {
        string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

        Assert.Contains("svc.NextScheduledWanRefresh.HasValue", source);
        Assert.Contains("svc.NextScheduledWanRefresh.Value.ToLocalTime().ToString(\"yyyy-MM-dd HH:mm:ss\")", source);
        Assert.DoesNotContain("svc.LastRefresh.Value.ToLocalTime().AddSeconds(60)", source);
    }

    [Fact]
    public void MainWindow_CompactNetworkFooter_PopupSupportsCopyingLanAndWanIp()
    {
        string source = File.ReadAllText(SourcePath("MultiPingMonitor", "UI", "MainWindow.xaml.cs"));

        Assert.Contains("AddCompactNetworkPopupCopyRow(", source);
        Assert.Contains("CopyCompactNetworkValueToClipboard", source);
        Assert.Contains("NetworkIdentity_ClickToCopy", source);
        Assert.Contains("NetworkIdentity_CopyFailed", source);
        Assert.Contains("Clipboard.SetText(value.Trim())", source);
        Assert.Contains("NetworkIdentity_WanCopied", source);
        Assert.Contains("NetworkIdentity_LanCopied", source);
        Assert.Contains("NetworkIdentity_WanUnavailable", source);
        Assert.Contains("NetworkIdentity_LanUnavailable", source);
        Assert.Contains("_compactNetworkFooterCopyToastPopup", source);
        Assert.Contains("Interval = TimeSpan.FromMilliseconds(1400)", source);
    }
    private static string SourcePath(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, Path.Combine(parts));
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not find source file.", Path.Combine(parts));
    }
}