namespace ServiceLib.Tests.Services;

public class DesktopUpdatesTests
{
    [Test]
    public async Task ClientIsExcludedEvenWhenOlderSettingsSelectIt()
    {
        var manager = CoreInfoManager.Instance;
        await manager.GetCheckUpdateCoreTypes().Contains(ECoreType.v2rayN).Should().BeFalse();
        var config = new Config { GuiItem = new(), CheckUpdateItem = new() { SelectedCoreTypes = ["v2rayN"] } };
        await CertPemManager.Instance.Init(config);
        using var http = new GeoSource();
        using var download = new DownloadService { Handler = http };
        var service = new UpdateService(download, config, (_, _) => Task.CompletedTask);
        await (await service.CheckHasUpdateOnlyAll()).Count.Should().BeEqualTo(0);
        await (await service.CheckHasUpdateOnly(ECoreType.v2rayN, true)).Success.Should().BeFalse();
        await http.Requests.Should().BeEqualTo(0);
    }

    [Test]
    public async Task GeoCheckDistinguishesEqualLengthRevisionsAndCommitsOnlyCompleteDownloads()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var names = new[] { "geoip.dat", "geosite.dat" };
            var files = names.Select(n => new FileDownloadRequest { FilePath = Path.Combine(dir.FullName, n), FileUrl = $"https://geo.test/{n}" }).ToList();
            foreach (var file in files) await File.WriteAllBytesAsync(file.FilePath, [1, 2, 3]);
            using var source = new GeoSource();
            await (await GeoFileUpdates.SyncAsync(source, files, false, default)).Should().BeTrue();
            source.FailOnSecond = true;
            try
            {
                await GeoFileUpdates.SyncAsync(source, files, true, default);
                Assert.Fail("Expected a failed download");
            }
            catch (HttpRequestException) { }
            await File.ReadAllBytes(files[0].FilePath).SequenceEqual(new byte[] { 1, 2, 3 }).Should().BeTrue();
            source.FailOnSecond = false;
            await GeoFileUpdates.SyncAsync(source, files, true, default);
            await (await GeoFileUpdates.SyncAsync(source, files, false, default)).Should().BeFalse();
            await File.ReadAllBytes(files[0].FilePath).SequenceEqual(new byte[] { 3, 2, 1 }).Should().BeTrue();
        }
        finally { dir.Delete(true); }
    }

    [Test]
    public async Task FailedBatchRestoresPreviouslyReplacedFiles()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var live = Path.Combine(dir.FullName, "core");
            var staged = live + ".new";
            File.WriteAllText(live, "installed");
            File.WriteAllText(staged, "candidate");
            try
            {
                StagedFileBatch.Apply([(staged, live), (Path.Combine(dir.FullName, "absent"), live + ".other")]);
                Assert.Fail("Expected an incomplete update to fail");
            }
            catch (IOException) { }
            await File.ReadAllText(live).Should().BeEqualTo("installed");
        }
        finally { dir.Delete(true); }
    }

    private sealed class GeoSource : HttpMessageHandler
    {
        public bool FailOnSecond { get; set; }
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(FailOnSecond && request.RequestUri!.AbsolutePath == "/geosite.dat"
                ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new ByteArrayContent([3, 2, 1]) });
        }
    }
}
