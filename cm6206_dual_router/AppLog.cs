using System.Diagnostics;
using System.Reflection;

namespace Cm6206DualRouter;

internal static class AppLog
{
    private static readonly object Gate = new();

    private const int MaxLogFiles = 16;
    private const string LatestLogFileName = "latest.log";
    private const string LegacyLogFileName = "app.log";

    public static string RootDirectory { get; private set; } = string.Empty;
    public static string LogsDirectory { get; private set; } = string.Empty;
    public static string CrashDirectory { get; private set; } = string.Empty;
    public static string LogPath { get; private set; } = string.Empty;

    public static void Initialize()
    {
        try
        {
            RootDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Cm6206DualRouter");

            LogsDirectory = Path.Combine(RootDirectory, "logs");
            CrashDirectory = Path.Combine(RootDirectory, "crash_logs");

            Directory.CreateDirectory(LogsDirectory);
            Directory.CreateDirectory(CrashDirectory);

            var latestPath = Path.Combine(LogsDirectory, LatestLogFileName);
            var legacyPath = Path.Combine(LogsDirectory, LegacyLogFileName);

            // One-time migration: older builds wrote to app.log.
            // If it exists, archive it so we converge on latest.log going forward.
            if (!File.Exists(latestPath) && File.Exists(legacyPath))
                RotateExistingLog(legacyPath, LogsDirectory);

            RotateExistingLog(latestPath, LogsDirectory);

            LogPath = latestPath;

            Info("==== startup ====");
            Info($"PID={Environment.ProcessId} Thread={Environment.CurrentManagedThreadId}");
            Info($"BaseDir={AppContext.BaseDirectory}");
            Info($"CurrentDir={Environment.CurrentDirectory}");
            Info($"CommandLine={Environment.CommandLine}");
            var ver = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "(unknown)";
            Info($"Version={ver}");

            PruneLogs(LogsDirectory);
        }
        catch
        {
            // never fail app startup due to logging
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
    {
        if (ex is null)
        {
            Write("ERROR", message);
            return;
        }

        Write("ERROR", message + Environment.NewLine + ex);
    }

    public static string Crash(Exception ex, string context)
    {
        try
        {
            var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
            var path = Path.Combine(CrashDirectory.Length == 0 ? Path.GetTempPath() : CrashDirectory,
                $"crash_{stamp}_{Environment.ProcessId}.txt");

            var text = $"Context: {context}{Environment.NewLine}" +
                       $"Time: {DateTimeOffset.Now:O}{Environment.NewLine}" +
                       $"PID: {Environment.ProcessId}{Environment.NewLine}" +
                       $"BaseDir: {AppContext.BaseDirectory}{Environment.NewLine}" +
                       $"CurrentDir: {Environment.CurrentDirectory}{Environment.NewLine}" +
                       $"CommandLine: {Environment.CommandLine}{Environment.NewLine}" +
                       Environment.NewLine + ex;

            File.WriteAllText(path, text);
            Error($"Crash written: {path}", ex);
            return path;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"[{DateTimeOffset.Now:O}] {level} {message}";

            lock (Gate)
            {
                if (!string.IsNullOrWhiteSpace(LogPath))
                    File.AppendAllText(LogPath, line + Environment.NewLine);
            }

            Debug.WriteLine(line);
        }
        catch
        {
            // ignore
        }
    }

    private static void RotateExistingLog(string currentLogPath, string logsDirectory)
    {
        try
        {
            if (!File.Exists(currentLogPath))
                return;

            // Name the archived log by when it was created (user-friendly, stable).
            var createdLocal = GetBestEffortCreatedLocal(currentLogPath);
            var stamp = createdLocal.ToString("yyyyMMdd_HHmmss_fff");
            var baseArchiveName = Path.Combine(logsDirectory, $"{stamp}.log");
            var archivePath = EnsureUniquePath(baseArchiveName);

            try
            {
                File.Move(currentLogPath, archivePath);
                return;
            }
            catch
            {
                // If the file is locked (another instance, AV, etc.), fall back to copy+truncate.
            }

            try
            {
                File.Copy(currentLogPath, archivePath, overwrite: false);
            }
            catch
            {
                // If we can't even copy it, there's nothing more we can safely do.
                return;
            }

            try
            {
                File.WriteAllText(currentLogPath, string.Empty);
            }
            catch
            {
                // ignore
            }
        }
        catch
        {
            // ignore
        }
    }

    private static DateTime GetBestEffortCreatedLocal(string path)
    {
        try
        {
            var t = File.GetCreationTime(path);
            if (t.Year >= 2000)
                return t;
        }
        catch
        {
            // ignore
        }

        try
        {
            var t = File.GetLastWriteTime(path);
            if (t.Year >= 2000)
                return t;
        }
        catch
        {
            // ignore
        }

        return DateTime.Now;
    }

    private static string EnsureUniquePath(string desiredPath)
    {
        try
        {
            if (!File.Exists(desiredPath))
                return desiredPath;

            var dir = Path.GetDirectoryName(desiredPath) ?? string.Empty;
            var file = Path.GetFileNameWithoutExtension(desiredPath);
            var ext = Path.GetExtension(desiredPath);

            for (var i = 2; i <= 9999; i++)
            {
                var candidate = Path.Combine(dir, $"{file}_{i}{ext}");
                if (!File.Exists(candidate))
                    return candidate;
            }
        }
        catch
        {
            // ignore
        }

        // Last resort.
        return desiredPath;
    }

    private static void PruneLogs(string logsDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(logsDirectory) || !Directory.Exists(logsDirectory))
                return;

            var files = Directory.EnumerateFiles(logsDirectory, "*.log")
                .Select(p =>
                {
                    try { return new FileInfo(p); }
                    catch { return null; }
                })
                .Where(f => f is not null)
                .Cast<FileInfo>()
                .ToList();

            if (files.Count <= MaxLogFiles)
                return;

            // Never delete the active latest log.
            var deletable = files
                .Where(f => !string.Equals(f.Name, LatestLogFileName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f =>
                {
                    try { return f.CreationTimeUtc; }
                    catch { return DateTime.MaxValue; }
                })
                .ToList();

            var total = files.Count;
            foreach (var f in deletable)
            {
                if (total <= MaxLogFiles)
                    break;

                try
                {
                    f.Delete();
                    total--;
                }
                catch
                {
                    // ignore
                }
            }
        }
        catch
        {
            // ignore
        }
    }
}
