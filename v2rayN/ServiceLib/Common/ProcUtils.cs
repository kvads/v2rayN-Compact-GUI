namespace ServiceLib.Common;

public static class ProcUtils
{
    private static readonly string _tag = "ProcUtils";

    public static void ProcessStart(string? fileName, string arguments = "")
    {
        _ = ProcessStart(fileName, arguments, null);
    }

    public static int? ProcessStart(string? fileName, string arguments, string? dir)
    {
        if (fileName.IsNullOrEmpty())
        {
            return null;
        }
        try
        {
            if (fileName.Contains(' '))
            {
                fileName = fileName.AppendQuotes();
            }
            if (arguments.Contains(' '))
            {
                arguments = arguments.AppendQuotes();
            }

            Process proc = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = dir ?? string.Empty
                }
            };
            _ = proc.Start();
            return dir is null ? null : proc.Id;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return null;
    }

    public static bool RebootAsAdmin(bool blAdmin = true)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                UseShellExecute = true,
                Arguments = $"{Global.RebootAs} {Environment.ProcessId}",
                WorkingDirectory = Utils.StartupPath(),
                FileName = Utils.GetExePath().AppendQuotes(),
                Verb = blAdmin ? "runas" : null,
            };
            return Process.Start(startInfo) != null;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return false;
        }
    }

    public static void WaitForPreviousInstance(string[] args)
    {
        if (args is not [Global.RebootAs, var pid] || !int.TryParse(pid, out var id) || id == Environment.ProcessId) return;
        try
        {
            // Let the old instance release its listening ports and finish saving its configuration.
            using var previous = Process.GetProcessById(id);
            using var current = Process.GetCurrentProcess();
            if (previous.ProcessName == current.ProcessName) previous.WaitForExit(15000);
        }
        catch (ArgumentException) { } // Already exited.
    }
}
