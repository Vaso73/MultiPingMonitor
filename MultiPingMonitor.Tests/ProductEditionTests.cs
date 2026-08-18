using MultiPingMonitor.Classes;

namespace MultiPingMonitor.Tests;

public sealed class ProductEditionTests
{
    [Fact]
    public void EditionCapabilities_AreInternallyConsistent()
    {
        Assert.NotEqual(
            MultiPingMonitorProductEdition.IsPublicFree,
            MultiPingMonitorProductEdition.IsSponsorPro);

        if (MultiPingMonitorProductEdition.IsPublicFree)
        {
            Assert.Equal("Public Free", MultiPingMonitorProductEdition.EditionName);
            Assert.False(MultiPingMonitorProductEdition.SupportsCompactMode);
            Assert.False(MultiPingMonitorProductEdition.SupportsCompactSets);
            Assert.False(MultiPingMonitorProductEdition.SupportsLivePing);
            Assert.False(MultiPingMonitorProductEdition.SupportsNetworkIdentity);
            Assert.False(MultiPingMonitorProductEdition.SupportsSponsorProUpdates);
            Assert.True(MultiPingMonitorProductEdition.SupportsSponsorProUpgrade);
            return;
        }

        Assert.Equal("Sponsor Pro", MultiPingMonitorProductEdition.EditionName);
        Assert.True(MultiPingMonitorProductEdition.SupportsCompactMode);
        Assert.True(MultiPingMonitorProductEdition.SupportsCompactSets);
        Assert.True(MultiPingMonitorProductEdition.SupportsLivePing);
        Assert.True(MultiPingMonitorProductEdition.SupportsNetworkIdentity);
        Assert.True(MultiPingMonitorProductEdition.SupportsSponsorProUpdates);
        Assert.False(MultiPingMonitorProductEdition.SupportsSponsorProUpgrade);
    }

    [Fact]
    public void PublicFreeBuildSymbol_IsConnectedToProjectProperty()
    {
        string root = FindRepositoryRoot();
        string project = File.ReadAllText(
            Path.Combine(root, "MultiPingMonitor", "MultiPingMonitor.csproj"));
        string source = File.ReadAllText(
            Path.Combine(root, "MultiPingMonitor", "Classes", "MultiPingMonitorProductEdition.cs"));

        Assert.Contains("MultiPingMonitorEdition)' == 'PublicFree", project);
        Assert.Contains("MULTIPINGMONITOR_FREE", project);
        Assert.Contains("#if MULTIPINGMONITOR_FREE", source);
    }

    [Fact]
    public void FreeBoundaries_CoverMenusCommandsStartupAndAboutUpgrade()
    {
        string root = FindRepositoryRoot();
        string main = File.ReadAllText(
            Path.Combine(root, "MultiPingMonitor", "UI", "MainWindow.xaml.cs"));
        string about = File.ReadAllText(
            Path.Combine(root, "MultiPingMonitor", "UI", "AboutWindow.xaml.cs"));
        string optionsXaml = File.ReadAllText(
            Path.Combine(root, "MultiPingMonitor", "UI", "OptionsWindow.xaml"));
        string optionsCode = File.ReadAllText(
            Path.Combine(root, "MultiPingMonitor", "UI", "OptionsWindow.xaml.cs"));

        Assert.Contains("EnforceProductEdition();", main);
        Assert.Contains("ToggleDisplayModeMenu.Visibility = Visibility.Collapsed", main);
        Assert.Contains("NewLivePingMenu.Visibility = Visibility.Collapsed", main);
        Assert.Contains("SupportsSponsorProUpdates", main);
        Assert.Contains("SupportsSponsorProUpgrade", about);
        Assert.Contains("About_SponsorProBenefits", about);
        Assert.Contains("About_InstallSponsorPro", about);
        Assert.Contains("x:Name=\"DisplayModeSettingsCard\"", optionsXaml);
        Assert.Contains("x:Name=\"NetworkIdentityAudioSettingsCard\"", optionsXaml);
        Assert.Contains("x:Name=\"AutomaticUpdateSettingsCard\"", optionsXaml);
        Assert.Contains("x:Name=\"StartupSettingsCard\"", optionsXaml);
        Assert.Contains("DisplayModeSettingsCard.Visibility", optionsCode);
        Assert.Contains("NetworkIdentityAudioSettingsCard.Visibility", optionsCode);
        Assert.Contains("AutomaticUpdateSettingsCard.Visibility", optionsCode);
        Assert.Contains("Grid.SetColumnSpan(StartupSettingsCard, 3)", optionsCode);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MultiPingMonitor.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
