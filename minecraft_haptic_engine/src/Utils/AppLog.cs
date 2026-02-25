using System.Diagnostics;
using System.Reflection;

namespace MinecraftHapticEngine.Utils;

internal static class AppLog
{
    private static readonly object Gate = new();

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
                "MinecraftHapticEngine");

            LogsDirectory = Path.Combine(RootDirectory, "logs");
            CrashDirectory = Path.Combine(RootDirectory, "crash_logs");

            Directory.CreateDirectory(LogsDirectory);
            Directory.CreateDirectory(CrashDirectory);

            LogPath = Path.Combine(LogsDirectory, "app.log");

            Info("==== startup ====");
            Info($"PID={Environment.ProcessId} Thread={Environment.CurrentManagedThreadId}");
            Info($"BaseDir={AppContext.BaseDirectory}");
            Info($"CurrentDir={Environment.CurrentDirectory}");
            Info($"CommandLine={Environment.CommandLine}");
            var ver = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "(unknown)";
            Info($"Version={ver}");
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
}
