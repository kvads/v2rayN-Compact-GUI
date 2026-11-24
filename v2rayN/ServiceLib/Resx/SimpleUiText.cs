namespace ServiceLib.Resx;

public static class SimpleUiText
{
    private static string Get(string key) => ResUI.ResourceManager.GetString(key, ResUI.Culture) ?? key;

    public static string CheckingUpdates => Get(nameof(CheckingUpdates));
    public static string InstallingUpdates => Get(nameof(InstallingUpdates));
    public static string InstallUpdates => Get(nameof(InstallUpdates));
    public static string PackagesCurrent => Get(nameof(PackagesCurrent));
    public static string UpdateFailed => Get(nameof(UpdateFailed));
    public static string NoUpdateTargets => Get(nameof(NoUpdateTargets));
    public static string SearchConfigurations => Get(nameof(SearchConfigurations));
    public static string RealDelayLabel => Get(nameof(RealDelayLabel));
    public static string NoConfigurations => Get(nameof(NoConfigurations));
    public static string ToggleProxy => Get(nameof(ToggleProxy));
    public static string SystemProxy => Get(nameof(SystemProxy));
    public static string ConnectionMode => Get(nameof(ConnectionMode));
    public static string TunModeLabel => Get(nameof(TunModeLabel));
    public static string ConnectionModeHint => Get(nameof(ConnectionModeHint));
    public static string OpenConfigurations => Get(nameof(OpenConfigurations));
    public static string ActiveConfiguration => Get(nameof(ActiveConfiguration));
    public static string SelectAnother => Get(nameof(SelectAnother));
    public static string Configurations => Get(nameof(Configurations));
    public static string AddFromClipboard => Get(nameof(AddFromClipboard));
    public static string AddConfiguration => Get(nameof(AddConfiguration));
    public static string AddConfigurationTray => "+  " + AddConfiguration;
    public static string PasteConfigurationLink => Get(nameof(PasteConfigurationLink));
    public static string Add => Get(nameof(Add));
    public static string Connected => Get(nameof(Connected));
    public static string Disconnected => Get(nameof(Disconnected));
    public static string Connect => Get(nameof(Connect));
    public static string Disconnect => Get(nameof(Disconnect));
}
