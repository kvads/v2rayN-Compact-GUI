namespace ServiceLib.Services;

public partial class UpdateService(DownloadService downloadService, Config config, Func<bool, string, Task> updateFunc)
{
    private readonly Config? _config = config;
    private readonly Func<bool, string, Task>? _updateFunc = updateFunc;
    private static readonly string _tag = "UpdateService";

    private GitHubReleaseSource ReleaseSource => field ??= new(downloadService, progress => UpdateFunc(false, GetProgressMsg(progress)));

    public async Task<UpdateResult> CheckHasUpdateOnly(ECoreType type, bool preRelease, CancellationToken cancellationToken = default)
    {
        if (!CoreInfoManager.Instance.IsCheckUpdateSupported(type))
        {
            return new UpdateResult(false, ResUI.MsgNotSupport);
        }

        var checkPreRelease = CoreInfoManager.Instance.GetCheckPreRelease(type, preRelease);
        return await CheckUpdateAsync(type, checkPreRelease, cancellationToken);
    }

    public async Task<List<string>> CheckHasUpdateOnlyAll(CancellationToken cancellationToken = default)
    {
        var msgs = new List<string>();
        foreach (var type in CoreInfoManager.Instance.GetCheckUpdateCoreTypes())
        {
            if (!(_config.CheckUpdateItem.SelectedCoreTypes?.Contains(type.ToString()) ?? true))
            {
                continue;
            }

            var preRelease = _config.CheckUpdateItem.CheckPreReleaseCoreTypes?.Contains(type.ToString()) ?? false;
            var result = await CheckHasUpdateOnly(type, preRelease, cancellationToken);
            if (result.Success && result.Version != null)
            {
                var msg = string.Format(ResUI.MsgCheckUpdateHasNewVersion, type, result.Version);
                msgs.Add(msg);
                AppManager.Instance.SetLastCheckUpdateResult(type, msg);
            }
            else
            {
                AppManager.Instance.SetLastCheckUpdateResult(type, result.Msg);
            }
        }
        return msgs;
    }

    public async Task UpdateGeoFileAll(CancellationToken cancellationToken = default)
    {
        var requests = await GetGeoRequests();
        await GeoFileUpdates.SyncAsync(downloadService.Handler, requests, true, cancellationToken);
        await UpdateFunc(true, string.Format(ResUI.MsgDownloadGeoFileSuccessfully, "geo"));
    }

    public async Task<bool> HasGeoUpdatesAsync(CancellationToken cancellationToken)
    {
        var requests = await GetGeoRequests();
        return await GeoFileUpdates.SyncAsync(downloadService.Handler, requests, false, cancellationToken);
    }

    private async Task<List<FileDownloadRequest>> GetGeoRequests() =>
        [.. GetGeoFilesRequest(), .. GetOtherFilesRequest(), .. await GetSrsFileAllRequest()];

    #region CheckUpdate private

    private async Task<UpdateResult> CheckUpdateAsync(ECoreType type, bool preRelease, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await GetRemoteVersion(type, preRelease, cancellationToken);
            if (!result.Success || result.Version is null)
            {
                return result;
            }
            return await ParseDownloadUrl(type, result, cancellationToken);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            await UpdateFunc(false, ex.Message);
            return new UpdateResult(false, ex.Message);
        }
    }

    private async Task<UpdateResult> GetRemoteVersion(ECoreType type, bool preRelease, CancellationToken cancellationToken = default)
    {
        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(type);
        if (coreInfo == null)
        {
            return new UpdateResult(false, "");
        }

        var tagName = await ReleaseSource.GetLatestTag(coreInfo, preRelease, cancellationToken);
        if (string.IsNullOrEmpty(tagName) || new SemanticVersion(tagName).Equals(new SemanticVersion(0, 0, 0)))
        {
            return new UpdateResult(false, "");
        }
        return new UpdateResult(true, new SemanticVersion(tagName));
    }

    private static string GetProgressMsg(GitHubReleaseProgress progress)
    {
        switch (progress.Stage)
        {
            case EGitHubReleaseStage.LatestRedirect:
                return string.Format(ResUI.MsgCheckUpdateViaLatest, progress.CoreType);
            case EGitHubReleaseStage.ReleasesApi:
                return string.Format(ResUI.MsgCheckUpdateViaApi, progress.CoreType);
            case EGitHubReleaseStage.ReleasesApiFailed:
                return string.Format(ResUI.MsgCheckUpdateViaApiFailed, progress.CoreType);
            case EGitHubReleaseStage.GitRefs:
                return string.Format(ResUI.MsgCheckUpdateViaGitRefs, progress.CoreType);
            case EGitHubReleaseStage.GitRefsFailed:
                return string.Format(ResUI.MsgCheckUpdateViaGitRefsFailed, progress.CoreType);
            default:
                return string.Empty;
        }
    }

    [GeneratedRegex(@"v?(?<version>\d+\.\d+\.\d+(?:-[0-9a-zA-Z.-]+)?(?:\+[0-9a-zA-Z.-]+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex SemVerRegex();

    public static async Task<SemanticVersion?> GetInstalledVersionAsync(ECoreType type, CancellationToken token = default)
    {
        var info = CoreInfoManager.Instance.GetCoreInfo(type);
        if (info == null || !CoreInfoManager.Instance.IsCheckUpdateSupported(type))
        {
            return null;
        }
        var executable = FindExecutable(info, Utils.GetBinPath("", type.ToString()));
        return executable == null ? null : await ReadVersionAsync(info, executable, token);
    }

    internal static string? FindExecutable(CoreInfo info, string directory) =>
        (info.CoreExes ?? []).Select(name => Path.Combine(directory, Utils.GetExeName(name))).FirstOrDefault(File.Exists);

    internal static async Task<SemanticVersion?> ReadVersionAsync(CoreInfo info, string executable, CancellationToken token)
    {
        var output = await Utils.GetCliWrapOutput(executable, info.VersionArg, token);
        var match = SemVerRegex().Match(output ?? "");
        return match.Success ? new SemanticVersion(match.Groups["version"].Value) : null;
    }

    private async Task<UpdateResult> ParseDownloadUrl(ECoreType type, UpdateResult result, CancellationToken cancellationToken = default)
    {
        try
        {
            var version = result.Version ?? new SemanticVersion(0, 0, 0);
            var coreInfo = CoreInfoManager.Instance.GetCoreInfo(type);
            var coreUrl = GetUrlFromCore(coreInfo);
            if (string.IsNullOrEmpty(coreUrl))
            {
                return new UpdateResult(false, ResUI.MsgNotSupport);
            }
            var installed = await GetInstalledVersionAsync(type, cancellationToken);
            if (installed != null && installed >= version)
            {
                var message = string.Format(ResUI.IsLatestCore, type, installed.ToStandardVersionString("v"));
                return new UpdateResult(false, message) { IsCurrent = true, Version = version };
            }
            var url = string.Format(coreUrl, version, version.ToString().RemovePrefix("v"));
            result.Url = url;
            return result;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            await UpdateFunc(false, ex.Message);
            return new UpdateResult(false, ex.Message);
        }
    }

    private static string? GetUrlFromCore(CoreInfo? coreInfo)
    {
        if (Utils.IsWindows())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => coreInfo?.DownloadUrlWinArm64,
                Architecture.X64 => coreInfo?.DownloadUrlWin64,
                _ => null,
            };
        }
        else if (Utils.IsLinux())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => coreInfo?.DownloadUrlLinuxArm64,
                Architecture.RiscV64 => coreInfo?.DownloadUrlLinuxRiscV64,
                Architecture.LoongArch64 => coreInfo?.DownloadUrlLinuxLoong64,
                Architecture.X64 => coreInfo?.DownloadUrlLinux64,
                _ => null,
            };
        }
        else if (Utils.IsMacOS())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => coreInfo?.DownloadUrlOSXArm64,
                Architecture.X64 => coreInfo?.DownloadUrlOSX64,
                _ => null,
            };
        }
        return null;
    }

    #endregion CheckUpdate private

    #region Geo private

    private List<FileDownloadRequest> GetGeoFilesRequest()
    {
        var geoUrl = string.IsNullOrEmpty(_config?.ConstItem.GeoSourceUrl)
            ? Global.GeoUrl
            : _config.ConstItem.GeoSourceUrl;

        List<string> files = ["geosite", "geoip"];
        return
        [
            .. from geoName in files
            let fileName = $"{geoName}.dat"
            let targetPath = Utils.GetBinPath($"{fileName}")
            let url = string.Format(geoUrl, geoName)
            select new FileDownloadRequest()
            {
                FileUrl = url,
                FilePath = targetPath,
                DisplayFileName = fileName,
            },
        ];
    }

    private List<FileDownloadRequest> GetOtherFilesRequest()
    {
        //If it is not in China area, no update is required
        if (_config.ConstItem.GeoSourceUrl.IsNotEmpty())
        {
            return [];
        }

        return
        [
            .. Global.OtherGeoUrls.Select(url =>
            {
                var fileName = Path.GetFileName(url);
                var targetPath = Utils.GetBinPath($"{fileName}");
                return new FileDownloadRequest()
                {
                    FileUrl = url,
                    FilePath = targetPath,
                    DisplayFileName = fileName,
                };
            }),
        ];
    }

    private async Task<List<FileDownloadRequest>> GetSrsFileAllRequest()
    {
        var geoipFiles = new List<string>();
        var geoSiteFiles = new List<string>();

        // Collect from routing rules
        var routingItems = await ConfigDbQuery.RoutingItems();
        foreach (var routing in routingItems)
        {
            var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet);
            foreach (var item in rules ?? [])
            {
                AddPrefixedItems(item.Ip, Global.GeoIPPrefix, geoipFiles);
                AddPrefixedItems(item.Domain, Global.GeoSitePrefix, geoSiteFiles);
            }
        }

        // Collect from DNS configuration
        var dnsItem = await ConfigDbQuery.GetDNSItem(ECoreType.sing_box);
        if (dnsItem != null)
        {
            ExtractDnsRuleSets(dnsItem.NormalDNS, geoipFiles, geoSiteFiles);
            ExtractDnsRuleSets(dnsItem.TunDNS, geoipFiles, geoSiteFiles);
        }

        // Append default items
        geoSiteFiles.AddRange(["google", "cn", "geolocation-cn", "category-ads-all"]);

        // Download files
        var path = Utils.GetBinPath("srss");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        return
        [
            .. geoipFiles.Distinct().Select(f => (type: "geoip", file: f))
                .Concat(geoSiteFiles.Distinct().Select(f => (type: "geosite", file: f)))
                .Select(item => GetSrsFileRequest(item.type, item.file)),
        ];
    }

    private void AddPrefixedItems(List<string>? items, string prefix, List<string> output)
    {
        if (items == null)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item.StartsWith(prefix))
            {
                output.Add(item.Substring(prefix.Length));
            }
        }
    }

    private void ExtractDnsRuleSets(string? dnsJson, List<string> geoipFiles, List<string> geoSiteFiles)
    {
        if (string.IsNullOrEmpty(dnsJson))
        {
            return;
        }

        try
        {
            var dns = JsonUtils.Deserialize<Dns4Sbox>(dnsJson);
            if (dns?.rules != null)
            {
                foreach (var rule in dns.rules)
                {
                    ExtractSrsRuleSets(rule, geoipFiles, geoSiteFiles);
                }
            }
        }
        catch { }
    }

    private void ExtractSrsRuleSets(Rule4Sbox? rule, List<string> geoipFiles, List<string> geoSiteFiles)
    {
        if (rule == null)
        {
            return;
        }

        AddPrefixedItems(rule.rule_set, "geosite-", geoSiteFiles);
        AddPrefixedItems(rule.rule_set, "geoip-", geoipFiles);

        // Handle nested rules recursively
        if (rule.rules != null)
        {
            foreach (var nestedRule in rule.rules)
            {
                ExtractSrsRuleSets(nestedRule, geoipFiles, geoSiteFiles);
            }
        }
    }

    private FileDownloadRequest GetSrsFileRequest(string type, string srsName)
    {
        var srsUrl = string.IsNullOrEmpty(_config.ConstItem.SrsSourceUrl)
                        ? Global.SingboxRulesetUrl
                        : _config.ConstItem.SrsSourceUrl;

        if (srsName.Contains('/') || srsName.Contains('\\') || srsName.Contains(':') || srsName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException("Invalid rule-set name.");
        }
        var fileName = $"{type}-{srsName}.srs";
        var targetPath = Path.Combine(Utils.GetBinPath("srss"), fileName);
        var url = string.Format(srsUrl, type, $"{type}-{srsName}", srsName);

        return new FileDownloadRequest()
        {
            FileUrl = url,
            FilePath = targetPath,
            DisplayFileName = fileName,
        };
    }

    #endregion Geo private

    private async Task UpdateFunc(bool notify, string msg)
    {
        await _updateFunc?.Invoke(notify, msg);
    }
}
