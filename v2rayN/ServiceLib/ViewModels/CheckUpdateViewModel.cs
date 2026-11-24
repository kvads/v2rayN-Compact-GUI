namespace ServiceLib.ViewModels;

public partial class CheckUpdateViewModel : MyReactiveObject
{
    private readonly SemaphoreSlim _gate = new(1);
    public EventChannel<RxVoid> ReloadRequested { get; } = new();
    public BulkObservableCollection<CheckUpdateModel> CheckUpdateModels { get; } = [];
    public ReactiveCommand<RxVoid, RxVoid> CheckUpdateCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> CheckOnlyCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> HomeUpdateCmd { get; }

    [Reactive] public partial bool EnableUpdateViaProxy { get; set; }
    [Reactive] public partial bool IsBusy { get; set; }
    [Reactive] public partial string HomeStatus { get; set; } = ResUI.menuCheckUpdate;
    [Reactive] public partial string HomeVersions { get; set; } = string.Empty;
    [Reactive] public partial string UpdateDetails { get; set; } = string.Empty;

    public CheckUpdateViewModel()
    {
        _config = ConfigTextHandler.Config;
        EnableUpdateViaProxy = _config.CheckUpdateItem.UpdateViaProxy;
        foreach (var type in CoreInfoManager.Instance.GetCheckUpdateCoreTypes())
        {
            CheckUpdateModels.Add(new()
            {
                CoreType = type,
                IsSelected = Selected(type.ToString()),
                IsCheckPreRelease = _config.CheckUpdateItem.CheckPreReleaseCoreTypes?.Contains(type.ToString()) == true,
                ShowCheckPreRelease = CoreInfoManager.Instance.GetCheckPreRelease(type, true)
            });
        }
        CheckUpdateModels.Add(new() { IsGeoFile = true, IsSelected = Selected("GeoFiles") });

        var ready = this.WhenAnyValue(vm => vm.IsBusy, busy => !busy);
        CheckOnlyCmd = ReactiveCommand.CreateFromTask(() => RunAsync(false, true), ready);
        CheckUpdateCmd = ReactiveCommand.CreateFromTask(() => RunAsync(true, true), ready);
        HomeUpdateCmd = ReactiveCommand.CreateFromTask(() => RunAsync(
            CheckUpdateModels.Any(row => row.IsSelected == true && row.Available), false, true), ready);

        this.WhenAnyValue(vm => vm.EnableUpdateViaProxy)
            .Where(value => value != _config.CheckUpdateItem.UpdateViaProxy)
            .SubscribeAsync(async value =>
            {
                _config.CheckUpdateItem.UpdateViaProxy = value;
                await ConfigTextHandler.SaveConfig();
            });
        AppEvents.CheckUpdatesRequested.AsObservable().ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async _ => await RunAsync(false, false));
    }

    private bool Selected(string name) => _config.CheckUpdateItem.SelectedCoreTypes?.Contains(name) ?? true;

    private async Task RunAsync(bool install, bool saveSelection, bool home = false)
    {
        if (!await _gate.WaitAsync(0)) return;
        IsBusy = true;
        try
        {
            var rows = CheckUpdateModels.Where(row => row.IsSelected == true).ToList();
            var targets = rows.Where(row => !home || row.Available).ToList();
            if (saveSelection)
            {
                _config.CheckUpdateItem.SelectedCoreTypes = rows.Select(row => row.CoreTypeForStorage).ToList();
                _config.CheckUpdateItem.CheckPreReleaseCoreTypes = CheckUpdateModels.Where(row => row.IsCheckPreRelease)
                    .Select(row => row.CoreTypeForStorage).ToList();
                await ConfigTextHandler.SaveConfig();
            }

            using var download = DownloadService.CreateDownloadServiceWithFallback(EnableUpdateViaProxy);
            if (!install || !home)
            {
                HomeStatus = SimpleUiText.CheckingUpdates;
                await RefreshLocalVersions();
                await CheckRows(rows, download);
            }
            if (install && targets.Any(row => row.Available))
            {
                var changes = targets.Where(row => row.Available).ToList();
                HomeStatus = SimpleUiText.InstallingUpdates;
                await InstallAsync(changes, EnableUpdateViaProxy);
                await RefreshLocalVersions();
                await CheckRows(rows, download);
            }
            RefreshStatus(rows);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(CheckUpdateViewModel), ex);
            HomeStatus = SimpleUiText.UpdateFailed;
            UpdateDetails = ex.GetBaseException().Message;
            NoticeManager.Instance.Enqueue(ex.Message);
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
        }
    }

    private async Task RefreshLocalVersions()
    {
        foreach (var row in CheckUpdateModels)
        {
            if (row.CoreType is {} type)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var version = await UpdateService.GetInstalledVersionAsync(type, timeout.Token);
                row.Installed = version?.ToStandardVersionString() ?? "—";
            }
            else
            {
                var path = Utils.GetBinPath("geosite.dat");
                row.Installed = File.Exists(path) ? File.GetLastWriteTime(path).ToString("yyyy-MM-dd") : "—";
            }
        }
        HomeVersions = string.Join("  ·  ", CheckUpdateModels.Select(row => $"{row.DisplayName}\u00a0{row.Installed}"));
    }

    private async Task CheckRows(List<CheckUpdateModel> rows, DownloadService download)
    {
        await Task.WhenAll(rows.Select(async row =>
        {
            row.Remarks = SimpleUiText.CheckingUpdates;
            row.Release = null;
            row.CheckFailed = false;
            row.Available = false;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(row.IsGeoFile ? 120 : 45));
                var service = new UpdateService(download, _config, (_, _) => Task.CompletedTask);
                if (row.CoreType is {} type)
                {
                    var check = await Task.Run(() => service.CheckHasUpdateOnly(type, row.IsCheckPreRelease, timeout.Token));
                    row.Release = check;
                    row.Available = check.Success && check.Url != null;
                    row.CheckFailed = !row.Available && !check.IsCurrent;
                    row.Remarks = row.Available ? $"{row.Installed} → {check.Version}"
                        : check.IsCurrent ? SimpleUiText.PackagesCurrent : check.Msg.IsNotEmpty() ? check.Msg : SimpleUiText.UpdateFailed;
                }
                else
                {
                    row.Available = await Task.Run(() => service.HasGeoUpdatesAsync(timeout.Token));
                    row.Remarks = row.Available ? SimpleUiText.InstallUpdates : SimpleUiText.PackagesCurrent;
                }
            }
            catch (Exception ex)
            {
                row.CheckFailed = true;
                row.Remarks = ex.Message;
            }
        }));
    }

    private void RefreshStatus(List<CheckUpdateModel> rows)
    {
        var count = rows.Count(row => row.Available);
        HomeStatus = count > 0 ? $"{SimpleUiText.InstallUpdates} ({count})"
            : rows.Count == 0 ? SimpleUiText.NoUpdateTargets
            : rows.Any(row => row.CheckFailed) ? SimpleUiText.UpdateFailed : SimpleUiText.PackagesCurrent;
        UpdateDetails = string.Join(Environment.NewLine, rows.Select(row => $"{row.DisplayName} {row.Installed} · {row.Remarks}"));
        AppEvents.HasUpdateNotified.Publish(count > 0);
    }

    private async Task InstallAsync(List<CheckUpdateModel> changes, bool viaProxy)
    {
        var packages = new List<DesktopCorePackage>();
        var stopped = false;
        var geoChanged = false;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
            using var download = DownloadService.CreateDownloadServiceWithFallback(viaProxy);
            foreach (var row in changes.Where(row => row.CoreType.HasValue))
            {
                row.Remarks = ResUI.Downloading;
                packages.Add(await Task.Run(() => DesktopCorePackage.PrepareAsync(row.CoreType!.Value, row.Release!, download, timeout.Token)));
            }
            if (changes.Any(row => row.IsGeoFile))
            {
                var service = new UpdateService(download, _config, (_, _) => Task.CompletedTask);
                await Task.Run(() => service.UpdateGeoFileAll(timeout.Token));
                geoChanged = true;
            }
            if (packages.Count > 0)
            {
                await CoreManager.Instance.CoreStop();
                stopped = true;
                StagedFileBatch.Apply(packages.SelectMany(package => package.Files));
            }
        }
        finally
        {
            if (stopped || geoChanged) ReloadRequested.Publish();
            foreach (var package in packages) package.Dispose();
        }
    }
}
