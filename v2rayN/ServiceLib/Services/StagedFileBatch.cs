namespace ServiceLib.Services;

internal static class StagedFileBatch
{
    // Each source is staged on the target filesystem. Keep the old files until
    // every replacement has succeeded, including across different core folders.
    internal static void Apply(IEnumerable<(string Source, string Target)> files)
    {
        var applied = new List<(string Target, string? Backup)>();
        try
        {
            foreach (var (source, target) in files)
            {
                string? backup = null;
                if (File.Exists(target))
                {
                    backup = target + "." + Guid.NewGuid().ToString("N") + ".bak";
                    File.Replace(source, target, backup);
                }
                else
                {
                    File.Move(source, target);
                }
                applied.Add((target, backup));
            }
        }
        catch (Exception installError)
        {
            var errors = new List<Exception> { installError };
            foreach (var (target, backup) in applied.AsEnumerable().Reverse())
            {
                try
                {
                    if (backup == null) File.Delete(target);
                    else File.Move(backup, target, true);
                }
                catch (Exception restoreError) { errors.Add(restoreError); }
            }
            if (errors.Count > 1) throw new AggregateException(errors);
            throw;
        }

        foreach (var (_, backup) in applied)
        {
            if (backup == null) continue;
            try { File.Delete(backup); }
            catch (IOException ex) { Logging.SaveLog(nameof(StagedFileBatch), ex); }
        }
    }
}
