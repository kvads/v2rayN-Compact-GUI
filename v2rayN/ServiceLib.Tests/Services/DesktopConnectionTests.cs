namespace ServiceLib.Tests.Services;

public class DesktopConnectionTests
{
    [Test]
    public async Task OfflineSelectionPersistsWithoutEnablingEitherMode()
    {
        var config = new Config { TunModeItem = new(), SystemProxyItem = new() };
        DesktopConnection.Read(config).SelectMode(true).Write(config);
        var selected = DesktopConnection.Read(config);
        await selected.PreferTun.Should().BeTrue();
        await selected.IsConnected.Should().BeFalse();
        await config.TunModeItem.EnableTun.Should().BeFalse();
        await config.SystemProxyItem.SysProxyType.Should().BeEqualTo(ESysProxyType.ForcedClear);
    }

    [Test]
    public async Task LiveSwitchAndReconnectKeepOneModeActive()
    {
        var proxy = new DesktopConnection(false, false, ESysProxyType.Pac);
        var tun = proxy.SelectMode(true);
        await tun.Should().BeEqualTo(new DesktopConnection(true, true, ESysProxyType.ForcedClear));
        await tun.IsConnected.Should().BeTrue();
        var off = tun.Connect(false);
        await off.IsConnected.Should().BeFalse();
        await off.Connect(true).Should().BeEqualTo(tun);
        await tun.SelectMode(false).Should().BeEqualTo(new DesktopConnection(false, false, ESysProxyType.ForcedChange));
        await tun.SelectProxy(ESysProxyType.Pac).Should().BeEqualTo(proxy);
    }

    [Test]
    public async Task ExistingTunConfigurationDeterminesInitialPreference()
    {
        var config = new Config { TunModeItem = new() { EnableTun = true }, SystemProxyItem = new() };
        var old = DesktopConnection.Read(config);
        await old.PreferTun.Should().BeTrue();
        await old.IsConnected.Should().BeTrue();
    }
}
