namespace ServiceLib.ViewModels;

public partial class StatusBarViewModel : MyReactiveObject
{
    public Interaction<string, RxVoid> SetClipboardDataInteraction { get; } = new();
    public Interaction<RxVoid, string?> PasswordInputInteraction { get; } = new();
    public Interaction<RxVoid, bool> ReloadConnectionInteraction { get; } = new();
    public Interaction<RxVoid, RxVoid> DispatcherRefreshIconInteraction { get; } = new();
    public EventChannel<bool> SubscriptionsUpdateRequested { get; } = new();
    public EventChannel<bool?> ShowHideWindowRequested { get; } = new();

    private static readonly Lazy<StatusBarViewModel> _instance = new(() => new());
    public static StatusBarViewModel Instance => _instance.Value;

    public EventChannel<RxVoid> ReloadRequested { get; } = new();
    public EventChannel<RxVoid> AddServerViaScanRequested { get; } = new();
    public EventChannel<RxVoid> AddServerViaClipboardRequested { get; } = new();

    #region ObservableCollection

    public BulkObservableCollection<RoutingItem> RoutingItems { get; } = [];

    [Reactive]
    public partial RoutingItem SelectedRouting { get; set; }

    #endregion ObservableCollection

    public ReactiveCommand<RxVoid, RxVoid> AddServerViaClipboardCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddServerViaScanCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubUpdateCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubUpdateViaProxyCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> CopyProxyCmdToClipboardCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> NotifyLeftClickCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ShowWindowCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> HideWindowCmd { get; }

    #region System Proxy

    [Reactive]
    public partial bool BlSystemProxyClear { get; set; }

    [Reactive]
    public partial bool BlSystemProxySet { get; set; }

    [Reactive]
    public partial bool BlSystemProxyNothing { get; set; }

    [Reactive]
    public partial bool BlSystemProxyPac { get; set; }

    public ReactiveCommand<RxVoid, RxVoid> SystemProxyClearCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SystemProxySetCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SystemProxyNothingCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SystemProxyPacCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ToggleConnectionCmd { get; }

    [Reactive]
    public partial int ConnectionModeIndex { get; set; }

    [Reactive]
    public partial bool CanChangeConnection { get; set; } = true;

    [Reactive]
    public partial int SystemProxySelected { get; set; }

    [Reactive]
    public partial bool IsConnected { get; set; }

    [Reactive]
    public partial string ConnectionActionText { get; set; } = SimpleUiText.Connect;

    [Reactive]
    public partial string ConnectionStatusText { get; set; } = SimpleUiText.Disconnected;

    [Reactive]
    public partial bool BlSystemProxyPacVisible { get; set; }

    #endregion System Proxy

    #region UI

    [Reactive]
    public partial string InboundDisplay { get; set; }

    [Reactive]
    public partial string InboundLanDisplay { get; set; }

    [Reactive]
    public partial string RunningServerDisplay { get; set; }

    [Reactive]
    public partial string RunningServerToolTipText { get; set; }

    [Reactive]
    public partial string RunningInfoDisplay { get; set; }

    [Reactive]
    public partial string SpeedProxyDisplay { get; set; }

    [Reactive]
    public partial string SpeedDirectDisplay { get; set; }

    [Reactive]
    public partial bool EnableTun { get; set; }

    [Reactive]
    public partial bool BlIsNonWindows { get; set; }

    #endregion UI

    public StatusBarViewModel()
    {
        _config = ConfigTextHandler.Config;
        SelectedRouting = new();
        RunningServerToolTipText = GetRunningServerToolTipText("-");
        BlSystemProxyPacVisible = Utils.IsWindows();
        BlIsNonWindows = Utils.IsNonWindows();

        var initial = DesktopConnection.Read(_config);
        if (initial.EnableTun)
        {
            initial = (initial with { PreferTun = true }).Connect(AllowEnableTun());
        }
        initial.Write(_config);
        RefreshConnectionState();

        #region WhenAnyValue && ReactiveCommand

        this.WhenAnyValue(x => x.SelectedRouting)
            .Where(y => y != null && !y.Remarks.IsNullOrEmpty())
            .SubscribeAsync(async _ => await RoutingSelectedChangedAsync());

        SystemProxySelected = (int)_config.SystemProxyItem.SysProxyType;
        this.WhenAnyValue(x => x.SystemProxySelected)
            .Where(y => y >= 0)
            .SubscribeAsync(async _ => await DoSystemProxySelected());

        this.WhenAnyValue(x => x.EnableTun)
            .Where(on => CanChangeConnection && on != _config.TunModeItem.EnableTun)
            .SubscribeAsync(async on => await ApplyConnection((DesktopConnection.Read(_config) with { PreferTun = on }).Connect(on)));

        this.WhenAnyValue(x => x.ConnectionModeIndex)
            .Where(index => CanChangeConnection && index is 0 or 1 && (index == 1) != _config.TunModeItem.PreferTun)
            .SubscribeAsync(async index => await ApplyConnection(DesktopConnection.Read(_config).SelectMode(index == 1)));

        CopyProxyCmdToClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await CopyProxyCmdToClipboard();
        });

        NotifyLeftClickCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            ShowHideWindowRequested.Publish(null);
            await Task.CompletedTask;
        });
        ShowWindowCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            ShowHideWindowRequested.Publish(true);
            await Task.CompletedTask;
        });
        HideWindowCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            ShowHideWindowRequested.Publish(false);
            await Task.CompletedTask;
        });

        AddServerViaClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await AddServerViaClipboard();
            });
        AddServerViaScanCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerViaScan();
        });
        SubUpdateCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess(false);
        });
        SubUpdateViaProxyCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess(true);
        });

        //System proxy
        SystemProxyClearCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetListenerType(ESysProxyType.ForcedClear);
        });
        SystemProxySetCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetListenerType(ESysProxyType.ForcedChange);
        });
        SystemProxyNothingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetListenerType(ESysProxyType.Unchanged);
        });
        SystemProxyPacCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetListenerType(ESysProxyType.Pac);
        });
        ToggleConnectionCmd = ReactiveCommand.CreateFromTask(
            () => ApplyConnection(DesktopConnection.Read(_config).Connect(!IsConnected)),
            this.WhenAnyValue(vm => vm.CanChangeConnection));

        #endregion WhenAnyValue && ReactiveCommand

        #region AppEvents

        AppEvents.DispatcherStatisticsRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async result => await UpdateStatistics(result));

        AppEvents.SysProxyChangeRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async result => await SetListenerType(result));

        #endregion AppEvents

        _ = Init();
    }

    private async Task Init()
    {
        await ConfigDbHandler.InitBuiltinRouting();
        await RefreshRoutingsMenu();
        await InboundDisplayStatus();
        await SysProxyHandler.UpdateSysProxy(_config, false);
    }

    private async Task CopyProxyCmdToClipboard()
    {
        var cmd = Utils.IsWindows() ? "set" : "export";
        var address = $"{Global.Loopback}:{AppManager.Instance.GetLocalPort(EInboundProtocol.socks)}";

        var sb = new StringBuilder();
        sb.AppendLine($"{cmd} http_proxy={Global.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} https_proxy={Global.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} all_proxy={Global.Socks5Protocol}{address}");
        sb.AppendLine("");
        sb.AppendLine($"{cmd} HTTP_PROXY={Global.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} HTTPS_PROXY={Global.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} ALL_PROXY={Global.Socks5Protocol}{address}");

        await SetClipboardDataInteraction.HandleSafe(sb.ToString());
    }

    private async Task AddServerViaClipboard()
    {
        AddServerViaClipboardRequested.Publish();
        await Task.Delay(1000);
    }

    private async Task AddServerViaScan()
    {
        AddServerViaScanRequested.Publish();
        await Task.Delay(1000);
    }

    private async Task UpdateSubscriptionProcess(bool blProxy)
    {
        SubscriptionsUpdateRequested.Publish(blProxy);
        await Task.Delay(1000);
    }

    public async Task RefreshServersBiz()
    {
        //display running server
        var running = await ConfigTextHandler.GetDefaultServer();
        if (running != null)
        {
            RunningServerDisplay = running.GetSummary();
            RunningServerToolTipText = GetRunningServerToolTipText(RunningServerDisplay);
        }
        else
        {
            RunningServerDisplay = SimpleUiText.NoConfigurations;
            RunningServerToolTipText = GetRunningServerToolTipText(RunningServerDisplay);
        }
    }

    private string GetRunningServerToolTipText(string serverInfo)
    {
        return Utils.IsLinux() ? Global.AppName : serverInfo;
    }

    public async Task<AvailabilityCheckResult?> TestServerAvailability()
    {
        var item = await ConfigTextHandler.GetDefaultServer();
        if (item == null)
        {
            return null;
        }

        await TestServerAvailabilitySub(ResUI.Speedtesting);

        var result = await Task.Run(ConnectionHandler.RunAvailabilityCheck);
        var msg = string.Format(ResUI.TestMeOutput, result.Time, result.Ip);

        var ip = result.GetValidIp();
        if (ip.IsNotEmpty())
        {
            ProfileExManager.Instance.SetTestIpInfo(item.IndexId, ip);
        }
        if (result.Time > 0)
        {
            ProfileExManager.Instance.SetTestDelay(item.IndexId, result.Time, ESpeedActionType.Realping);
        }

        NoticeManager.Instance.SendMessageEx(msg);
        await TestServerAvailabilitySub(msg);
        return result;
    }

    private async Task TestServerAvailabilitySub(string msg)
    {
        RxSchedulers.MainThreadScheduler.Schedule(() =>
        {
            _ = TestServerAvailabilityResult(msg);
        });
        await Task.CompletedTask;
    }

    public async Task TestServerAvailabilityResult(string msg)
    {
        RunningInfoDisplay = msg;
        await Task.CompletedTask;
    }

    #region System proxy and Routings

    private Task SetListenerType(ESysProxyType type) =>
        ApplyConnection(DesktopConnection.Read(_config).SelectProxy(type));

    private async Task ApplyConnection(DesktopConnection next)
    {
        var previous = DesktopConnection.Read(_config);
        if (!CanChangeConnection || next == previous) return;
        CanChangeConnection = false;
        try
        {
            if (next.IsConnected && await ConfigTextHandler.GetDefaultServer() == null)
            {
                NoticeManager.Instance.Enqueue(ResUI.CheckServerSettings);
                return;
            }
            if (next.EnableTun && !previous.EnableTun && !AllowEnableTun())
            {
                if (Utils.IsWindows())
                {
                    // The elevated instance reads these settings at startup.
                    next.Write(_config);
                    if (await ConfigTextHandler.SaveConfig() != 0)
                    {
                        previous.Write(_config);
                        NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
                        return;
                    }
                    if (!await AppManager.Instance.RebootAsAdmin())
                    {
                        previous.Write(_config);
                        await ConfigTextHandler.SaveConfig();
                    }
                    return;
                }

                ShowHideWindowRequested.Publish(true);
                if ((await PasswordInputInteraction.HandleSafe(RxVoid.Default)).IsNullOrEmpty()) return;
            }

            next.Write(_config);
            if (next.EnableTun != previous.EnableTun)
            {
                var running = await ReloadConnectionInteraction.HandleSafe(RxVoid.Default);
                if (!running && next.IsConnected)
                {
                    // Restore the old core and proxy when a live handover fails.
                    previous.Write(_config);
                    if (!await ReloadConnectionInteraction.HandleSafe(RxVoid.Default))
                    {
                        previous.Connect(false).Write(_config);
                        if (previous.EnableTun) await ReloadConnectionInteraction.HandleSafe(RxVoid.Default);
                    }
                    NoticeManager.Instance.Enqueue(ResUI.FailedToRunCore);
                }
            }
            if (previous.Proxy != _config.SystemProxyItem.SysProxyType)
            {
                await SysProxyHandler.UpdateSysProxy(_config, false);
                NoticeManager.Instance.SendMessageEx($"{ResUI.TipChangeSystemProxy} - {_config.SystemProxyItem.SysProxyType}");
            }
            if (await ConfigTextHandler.SaveConfig() != 0) NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
        finally
        {
            RefreshConnectionState();
            CanChangeConnection = true;
        }
        if (next.EnableTun != previous.EnableTun || next.Proxy != previous.Proxy)
        {
            await DispatcherRefreshIconInteraction.HandleSafe(RxVoid.Default);
        }
    }

    private void RefreshConnectionState()
    {
        var state = DesktopConnection.Read(_config);
        var type = state.Proxy;
        EnableTun = state.EnableTun;
        ConnectionModeIndex = state.PreferTun ? 1 : 0;
        SystemProxySelected = (int)type;
        BlSystemProxyClear = type == ESysProxyType.ForcedClear;
        BlSystemProxySet = type == ESysProxyType.ForcedChange;
        BlSystemProxyNothing = type == ESysProxyType.Unchanged;
        BlSystemProxyPac = type == ESysProxyType.Pac;
        IsConnected = state.IsConnected;
        ConnectionActionText = IsConnected ? SimpleUiText.Disconnect : SimpleUiText.Connect;
        ConnectionStatusText = IsConnected ? SimpleUiText.Connected : SimpleUiText.Disconnected;
    }

    internal Task DisconnectWithoutProfileAsync()
    {
        var state = DesktopConnection.Read(_config);
        return state.IsConnected ? ApplyConnection(state.Connect(false)) : Task.CompletedTask;
    }

    internal async Task DisconnectFailedTunAsync()
    {
        if (!CanChangeConnection || !_config.TunModeItem.EnableTun) return;
        NoticeManager.Instance.Enqueue(ResUI.FailedToRunCore);
        await ApplyConnection(DesktopConnection.Read(_config).Connect(false));
    }

    public async Task RefreshRoutingsMenu()
    {
        var routings = await ConfigDbQuery.RoutingItems();

        RoutingItems.ReplaceRange(routings);

        SelectedRouting = routings.FirstOrDefault(t => t.IsActive == true);
    }

    private async Task RoutingSelectedChangedAsync()
    {
        if (SelectedRouting == null)
        {
            return;
        }

        var item = await ConfigDbQuery.GetRoutingItem(SelectedRouting?.Id);
        if (item is null)
        {
            return;
        }

        if (await ConfigDbHandler.SetDefaultRouting(item) == 0)
        {
            NoticeManager.Instance.SendMessageEx(ResUI.TipChangeRouting);
            ReloadRequested.Publish();
            await DispatcherRefreshIconInteraction.HandleSafe(RxVoid.Default);
        }
    }

    private async Task DoSystemProxySelected()
    {
        if (_config.SystemProxyItem.SysProxyType == (ESysProxyType)SystemProxySelected)
        {
            return;
        }
        await SetListenerType((ESysProxyType)SystemProxySelected);
    }

    private bool AllowEnableTun()
    {
        if (Utils.IsWindows())
        {
            return Utils.IsAdministrator();
        }
        else if (Utils.IsLinux())
        {
            return AppManager.Instance.LinuxSudoPwd.IsNotEmpty();
        }
        else if (Utils.IsMacOS())
        {
            return AppManager.Instance.LinuxSudoPwd.IsNotEmpty();
        }
        return false;
    }

    #endregion System proxy and Routings

    #region UI

    public async Task InboundDisplayStatus()
    {
        StringBuilder sb = new();
        sb.Append($"[{EInboundProtocol.mixed}:{AppManager.Instance.GetLocalPort(EInboundProtocol.socks)}");
        if (_config.Inbound.First().SecondLocalPortEnabled)
        {
            sb.Append($",{AppManager.Instance.GetLocalPort(EInboundProtocol.socks2)}");
        }
        sb.Append(']');
        InboundDisplay = $"{ResUI.LabLocal}:{sb}";

        if (_config.Inbound.First().AllowLANConn)
        {
            var lan = _config.Inbound.First().NewPort4LAN
                ? $"[{EInboundProtocol.mixed}:{AppManager.Instance.GetLocalPort(EInboundProtocol.socks3)}]"
                : $"[{EInboundProtocol.mixed}:{AppManager.Instance.GetLocalPort(EInboundProtocol.socks)}]";
            InboundLanDisplay = $"{ResUI.LabLAN}:{lan}";
        }
        else
        {
            InboundLanDisplay = $"{ResUI.LabLAN}:{Global.None}";
        }
        await Task.CompletedTask;
    }

    public async Task UpdateStatistics(ServerSpeedItem update)
    {
        if (!_config.GuiItem.DisplayRealTimeSpeed)
        {
            return;
        }

        try
        {
            if (AppManager.Instance.IsRunningCore(ECoreType.sing_box))
            {
                SpeedProxyDisplay = string.Format(ResUI.SpeedDisplayText, EInboundProtocol.mixed, Utils.HumanFy(update.ProxyUp), Utils.HumanFy(update.ProxyDown));
                SpeedDirectDisplay = string.Empty;
            }
            else
            {
                SpeedProxyDisplay = string.Format(ResUI.SpeedDisplayText, Global.ProxyTag, Utils.HumanFy(update.ProxyUp), Utils.HumanFy(update.ProxyDown));
                SpeedDirectDisplay = string.Format(ResUI.SpeedDisplayText, Global.DirectTag, Utils.HumanFy(update.DirectUp), Utils.HumanFy(update.DirectDown));
            }
        }
        catch
        {
        }
        await Task.CompletedTask;
    }

    #endregion UI
}
