using System.CommandLine;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace BetterSoundLauncher;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        AppLog.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            var path = AppLog.Crash(e.Exception, "WinForms UI thread");
            TryShowCrashDialog(path, e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                var path = AppLog.Crash(ex, "AppDomain.CurrentDomain.UnhandledException");
                TryShowCrashDialog(path, ex);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            var path = AppLog.Crash(e.Exception, "TaskScheduler.UnobservedTaskException");
            TryShowCrashDialog(path, e.Exception);
        };

        var routerConfigOption = new Option<string>(
            name: "--router-config",
            description: "Path to router config JSON",
            getDefaultValue: () => "router.json");

        var engineConfigOption = new Option<string>(
            name: "--engine-config",
            description: "Path to haptic engine config JSON",
            getDefaultValue: () => Path.Combine("config", "engine.json"));

        var wsOverride = new Option<string?>(
            name: "--ws",
            description: "Override WebSocket URL for the haptic engine (e.g. ws://127.0.0.1:7117/)");

        var root = new RootCommand("Better Sound Launcher (starts Router UI + Minecraft Haptic Engine)");
        root.AddOption(routerConfigOption);
        root.AddOption(engineConfigOption);
        root.AddOption(wsOverride);

        root.SetHandler((routerConfig, engineConfig, ws) =>
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new LauncherContext(routerConfig, engineConfig, ws));
        }, routerConfigOption, engineConfigOption, wsOverride);

        try
        {
            return root.Invoke(args);
        }
        catch (Exception ex)
        {
            var path = AppLog.Crash(ex, "Program.Main");
            TryShowCrashDialog(path, ex);
            return 1;
        }
    }

    private static void TryShowCrashDialog(string crashPath, Exception ex)
    {
        try
        {
            var where = string.IsNullOrWhiteSpace(crashPath) ? "(no crash log path available)" : crashPath;
            MessageBox.Show(
                $"BetterSoundLauncher crashed.\n\nCrash log:\n{where}\n\nError:\n{ex.Message}",
                "BetterSoundLauncher crashed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // ignore
        }
    }

    private sealed class LauncherContext : ApplicationContext
    {
        private readonly string _routerConfig;
        private readonly string _engineConfig;
        private readonly string? _ws;

        private Process? _router;
        private Process? _engine;
        private int _exitCode;

        public LauncherContext(string routerConfig, string engineConfig, string? ws)
        {
            _routerConfig = routerConfig;
            _engineConfig = engineConfig;
            _ws = ws;

            try
            {
                StartBoth();
            }
            catch (Exception ex)
            {
                AppLog.Error("Failed to start child processes", ex);
                MessageBox.Show(
                    $"Failed to start apps.\n\n{ex.Message}\n\nSee: {AppLog.LogPath}",
                    "Launcher error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                _exitCode = 1;
                ExitThread();
            }
        }

        protected override void ExitThreadCore()
        {
            StopBoth();
            Environment.ExitCode = _exitCode;
            base.ExitThreadCore();
        }

        private void StartBoth()
        {
            var baseDir = AppContext.BaseDirectory;

            var routerExe = Path.Combine(baseDir, "Cm6206DualRouter.exe");
            var engineExe = Path.Combine(baseDir, "MinecraftHapticEngine.exe");

            if (!File.Exists(routerExe))
                throw new FileNotFoundException($"Router executable not found: {routerExe}");

            if (!File.Exists(engineExe))
                throw new FileNotFoundException($"Engine executable not found: {engineExe}");

            var routerConfigPath = MakeAbsolute(baseDir, _routerConfig);
            var engineConfigPath = MakeAbsolute(baseDir, _engineConfig);

            if (!File.Exists(routerConfigPath))
                throw new FileNotFoundException($"Router config not found: {routerConfigPath}");

            if (!File.Exists(engineConfigPath))
                throw new FileNotFoundException($"Engine config not found: {engineConfigPath}");

            var routerArgs = $"--ui --config {Quote(routerConfigPath)}";

            var engineArgsBuilder = new StringBuilder();
            engineArgsBuilder.Append("--config ");
            engineArgsBuilder.Append(Quote(engineConfigPath));
            if (!string.IsNullOrWhiteSpace(_ws))
            {
                engineArgsBuilder.Append(" --ws ");
                engineArgsBuilder.Append(Quote(_ws));
            }

            var engineArgs = engineArgsBuilder.ToString();

            _router = StartProcess(routerExe, routerArgs, baseDir, tag: "router");
            _engine = StartProcess(engineExe, engineArgs, baseDir, tag: "engine");

            _router.Exited += (_, _) => OnChildExited("router", _router.ExitCode);
            _engine.Exited += (_, _) => OnChildExited("engine", _engine.ExitCode);

            AppLog.Info("Both processes started.");
        }

        private void OnChildExited(string which, int exitCode)
        {
            try
            {
                AppLog.Warn($"Child exited: {which} exitCode={exitCode}");

                // If either app closes, stop the other and exit the launcher.
                _exitCode = exitCode;
                ExitThread();
            }
            catch
            {
                // ignore
            }
        }

        private void StopBoth()
        {
            try
            {
                StopProcess(_engine, "engine");
            }
            catch
            {
                // ignore
            }

            try
            {
                StopProcess(_router, "router");
            }
            catch
            {
                // ignore
            }
        }

        private static void StopProcess(Process? p, string tag)
        {
            if (p is null)
                return;

            try
            {
                if (p.HasExited)
                    return;

                AppLog.Info($"Stopping {tag} pid={p.Id}");

                // Router is a WinForms app; try a clean close first.
                if (p.CloseMainWindow())
                {
                    if (p.WaitForExit(1500))
                        return;
                }

                p.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Failed to stop {tag}", ex);
            }
        }

        private static Process StartProcess(string exePath, string args, string workingDir, string tag)
        {
            AppLog.Info($"Starting {tag}: {exePath} {args}");

            var psi = new ProcessStartInfo(exePath, args)
            {
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var p = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            if (!p.Start())
                throw new InvalidOperationException($"Failed to start {tag} process");

            _ = Task.Run(() => Pump(p.StandardOutput, line => AppLog.Info($"{tag}: {line}")));
            _ = Task.Run(() => Pump(p.StandardError, line => AppLog.Warn($"{tag} [err]: {line}")));

            return p;
        }

        private static void Pump(StreamReader reader, Action<string> onLine)
        {
            try
            {
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line is null)
                        break;
                    if (line.Length == 0)
                        continue;
                    onLine(line);
                }
            }
            catch
            {
                // ignore
            }
        }

        private static string MakeAbsolute(string baseDir, string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            return Path.GetFullPath(Path.Combine(baseDir, path));
        }

        private static string Quote(string s)
        {
            if (s.Contains(' '))
                return "\"" + s + "\"";

            return s;
        }
    }
}
