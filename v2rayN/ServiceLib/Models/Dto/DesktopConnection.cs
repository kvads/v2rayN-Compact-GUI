namespace ServiceLib.Models.Dto;

internal readonly record struct DesktopConnection(bool PreferTun, bool EnableTun, ESysProxyType Proxy)
{
    public bool IsConnected => EnableTun || Proxy is ESysProxyType.ForcedChange or ESysProxyType.Pac;

    public static DesktopConnection Read(Config config) => new(
        config.TunModeItem.PreferTun ?? config.TunModeItem.EnableTun,
        config.TunModeItem.EnableTun, config.SystemProxyItem.SysProxyType);

    public void Write(Config config)
    {
        config.TunModeItem.PreferTun = PreferTun;
        config.TunModeItem.EnableTun = EnableTun;
        config.SystemProxyItem.SysProxyType = Proxy;
    }

    public DesktopConnection SelectMode(bool tun) =>
        IsConnected ? (this with { PreferTun = tun }).Connect(true) : this with { PreferTun = tun };

    public DesktopConnection Connect(bool on) => new(PreferTun, on && PreferTun,
        on && !PreferTun ? ESysProxyType.ForcedChange : ESysProxyType.ForcedClear);

    public DesktopConnection SelectProxy(ESysProxyType type) =>
        new(type == ESysProxyType.ForcedClear && PreferTun, false, type);
}
