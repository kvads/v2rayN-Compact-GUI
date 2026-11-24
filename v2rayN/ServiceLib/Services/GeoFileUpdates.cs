namespace ServiceLib.Services;

internal static class GeoFileUpdates
{
    private static readonly SemaphoreSlim Gate = new(1);

    // Geo sources can mix releases, rolling branches and custom URLs. Compare the
    // configured files, rather than treating a download date as a release number.
    internal static async Task<bool> SyncAsync(HttpMessageHandler handler, List<FileDownloadRequest> files,
        bool install, CancellationToken token)
    {
        await Gate.WaitAsync(token);
        var staged = new List<(string Temp, FileDownloadRequest Request, string? Validator)>();
        try
        {
            using var client = new HttpClient(handler, false);
            var cachePath = Utils.GetTempPath("geo-validators.json");
            var cache = File.Exists(cachePath)
                ? JsonUtils.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(cachePath, token)) ?? []
                : new Dictionary<string, string>();
            var changed = false;
            foreach (var file in files.DistinctBy(f => f.FilePath))
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromMinutes(2));
                using var response = await client.GetAsync(file.FileUrl, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                response.EnsureSuccessStatusCode();
                var content = response.Content;
                if (content.Headers.ContentType?.MediaType == "text/html" || content.Headers.ContentLength == 0)
                {
                    throw new InvalidDataException(file.FileName);
                }
                var validator = response.Headers.ETag?.ToString() ?? content.Headers.LastModified?.ToString("R");
                var local = new FileInfo(file.FilePath);
                if (local.Exists && local.Length > 0 && validator != null &&
                    cache.GetValueOrDefault(file.FilePath) == Stamp(file.FileUrl, local, validator))
                {
                    continue;
                }

                await using var body = await content.ReadAsStreamAsync(deadline.Token);
                if (!install)
                {
                    if (!local.Exists || (content.Headers.ContentLength is long length && length != local.Length)
                        || !await MatchesAsync(local, body, deadline.Token))
                    {
                        changed = true;
                        break;
                    }
                    if (validator != null)
                    {
                        cache[file.FilePath] = Stamp(file.FileUrl, local, validator);
                    }
                }
                else
                {
                    var temp = file.FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    staged.Add((temp, file, validator));
                    await using var target = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    await body.CopyToAsync(target, deadline.Token);
                    if (target.Length == 0 || (content.Headers.ContentLength is long expected && target.Length != expected))
                    {
                        throw new EndOfStreamException(file.FileName);
                    }
                    changed = true;
                }
            }

            StagedFileBatch.Apply(staged.Select(f => (f.Temp, f.Request.FilePath)));
            foreach (var (_, file, validator) in staged)
            {
                if (validator != null)
                {
                    cache[file.FilePath] = Stamp(file.FileUrl, new FileInfo(file.FilePath), validator);
                }
            }
            await File.WriteAllTextAsync(cachePath, JsonUtils.Serialize(cache), token);
            return changed;
        }
        finally
        {
            try
            {
                foreach (var (temp, _, _) in staged)
                {
                    File.Delete(temp);
                }
            }
            finally { Gate.Release(); }
        }
    }

    private static string Stamp(string url, FileInfo file, string validator) =>
        $"{url}\n{file.Length}\n{file.LastWriteTimeUtc.Ticks}\n{validator}";

    private static async Task<bool> MatchesAsync(FileInfo local, Stream remote, CancellationToken token)
    {
        await using var input = local.OpenRead();
        var left = new byte[65536];
        var right = new byte[left.Length];
        while (true)
        {
            var count = await remote.ReadAtLeastAsync(right, right.Length, false, token);
            var read = await input.ReadAtLeastAsync(left, left.Length, false, token);
            if (count != read || !left.AsSpan(0, read).SequenceEqual(right.AsSpan(0, count))) return false;
            if (count == 0) return true;
        }
    }
}
