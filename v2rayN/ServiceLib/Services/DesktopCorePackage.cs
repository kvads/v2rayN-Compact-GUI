namespace ServiceLib.Services;

// All writes go to a core's own bin directory. Prepare and verify the executable
// before stopping the running core, and keep originals until the batch commits.
internal sealed class DesktopCorePackage : IDisposable
{
    private readonly string _root;
    private readonly string _install;
    public IReadOnlyList<(string Source, string Target)> Files => _files;
    private readonly List<(string Source, string Target)> _files = [];

    private DesktopCorePackage(ECoreType type)
    {
        _install = Utils.GetBinPath("", type.ToString());
        _root = Path.Combine(_install, ".update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public static async Task<DesktopCorePackage> PrepareAsync(ECoreType type, UpdateResult release,
        DownloadService download, CancellationToken token)
    {
        if (!CoreUpdatePackageStager.SupportsCore(type) || !release.Success || release.Url is null || release.Version is null)
        {
            throw new InvalidOperationException(ResUI.MsgNotSupport);
        }
        var package = new DesktopCorePackage(type);
        try
        {
            var url = release.Url;
            var ext = url.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ? ".tar.gz" : Path.GetExtension(new Uri(url).AbsolutePath);
            var archive = Path.Combine(package._root, "download" + ext);
            var stage = Path.Combine(package._root, "package");
            Exception? failure = null;
            await download.DownloaderHelper.DownloadFileAsync(new() { FileUrl = url, FilePath = archive },
                progress => { if (progress.Error != null) failure = progress.Error; }, token);
            if (failure != null) throw new IOException(ResUI.OperationFailed, failure);

            await CoreUpdatePackageStager.ExtractAsync(type, archive, stage, token);
            var info = CoreInfoManager.Instance.GetCoreInfo(type)!;
            var executable = UpdateService.FindExecutable(info, stage) ?? throw new InvalidDataException(ResUI.FailedToRunCore);
            if (!OperatingSystem.IsWindows()) Utils.SetUnixFileMode(executable);
            var version = await UpdateService.ReadVersionAsync(info, executable, token);
            if (!release.Version.Equals(version)) throw new InvalidDataException($"{type}: {version} ≠ {release.Version}");

            var previous = UpdateService.FindExecutable(info, package._install);
            foreach (var file in Directory.GetFiles(stage))
            {
                var target = file == executable && previous != null ? previous : Path.Combine(package._install, Path.GetFileName(file));
                package._files.Add((file, target));
            }
            return package;
        }
        catch
        {
            package.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            try { Directory.Delete(_root, true); }
            catch (IOException ex) { Logging.SaveLog(nameof(DesktopCorePackage), ex); }
        }
    }
}
