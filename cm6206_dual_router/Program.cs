using System.CommandLine;
using System.CommandLine.Invocation;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        AppLog.Initialize();

        SingleInstanceCoordinator? singleInstance = null;

        try
        {
            AppLog.Info($"Args(original)={string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}");
        }
        catch
        {
            // ignore
        }

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

        // Double-click friendly: with no args, launch the UI instead of running headless
        // (headless mode is still available by providing --config without --ui).
        if (args.Length == 0)
            args = ["--ui"];

        // Ensure only a single instance runs. If a second instance is launched, signal the
        // primary instance to bring its window to front and then exit immediately.
        try
        {
            const string appId = "Cm6206DualRouter";
            if (!SingleInstanceCoordinator.TryCreate(appId, out singleInstance))
            {
                // Best-effort activation; even if it fails, still exit.
                try
                {
                    var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
                    var pipeName = $"{appId}:{sid}";
                    using var client = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.Out);
                    client.Connect(250);
                    client.Write(System.Text.Encoding.UTF8.GetBytes("activate\n"));
                    client.Flush();
                }
                catch
                {
                    // ignore
                }

                AppLog.Info("Another instance is already running; exiting.");
                return 0;
            }
        }
        catch (Exception ex)
        {
            // If single-instance enforcement fails unexpectedly, continue rather than
            // blocking startup.
            AppLog.Warn($"Single-instance check failed: {ex.Message}");
        }

        try
        {
            AppLog.Info($"Args(effective)={string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}");
        }
        catch
        {
            // ignore
        }

        var configOption = new Option<string>(
            name: "--config",
            description: "Path to router.json",
            getDefaultValue: () => "router.json");

        var listDevicesOption = new Option<bool>(
            name: "--list-devices",
            description: "List available playback devices and exit");

        var showFormatsOption = new Option<bool>(
            name: "--show-formats",
            description: "With --list-devices, also show each endpoint's Windows mix format (sample rate/channels)");

        var uiOption = new Option<bool>(
            name: "--ui",
            description: "Launch the UI");

        var probeCmvadrOption = new Option<bool>(
            name: "--probe-cmvadr",
            description: "Probe the CMVADR virtual driver endpoints (\\.\\CMVADR_Game / \\.\\CMVADR_Shaker) and print formats");

        var validateOption = new Option<bool>(
            name: "--validate",
            description: "Validate config + devices by constructing and starting the router briefly, then printing input status and exiting");

        var runForSecondsOption = new Option<int?>(
            name: "--run-for-sec",
            description: "Run for N seconds then stop automatically (headless mode)");

        var statusIntervalMsOption = new Option<int>(
            name: "--status-interval-ms",
            getDefaultValue: () => 1000,
            description: "In headless mode, print input status every N ms (0 disables)");

        var exportUiBundleOption = new Option<string?>(
            name: "--export-ui-bundle",
            description: "Export a Minecraft UI bundle folder containing Neon style/tokens/assets (neon_style.json, neon_animation.json, arrow icons) and exit");

        var root = new RootCommand("CM6206 Dual Virtual Router (2 virtual outputs -> 1 CM6206 7.1)");
        root.AddOption(configOption);
        root.AddOption(listDevicesOption);
        root.AddOption(showFormatsOption);
        root.AddOption(uiOption);
        root.AddOption(probeCmvadrOption);
        root.AddOption(validateOption);
        root.AddOption(runForSecondsOption);
        root.AddOption(statusIntervalMsOption);
        root.AddOption(exportUiBundleOption);

        static string FormatEndpoint(WasapiDualRouter.EndpointStatus ep)
        {
            var fillPct = ep.BufferLengthBytes <= 0 ? 0.0 : (100.0 * ep.BufferedBytes / ep.BufferLengthBytes);
            var last = ep.LastDataUtc is null ? "(never)" : ep.LastDataUtc.Value.ToLocalTime().ToString("HH:mm:ss");
            var conn = ep.Connected ? "Connected" : "Error";
            var fmt = $"{ep.SampleRate} Hz / {ep.BitsPerSample}-bit / {ep.Channels}ch";

            var nudges = (ep.TotalNudgeDropFrames > 0 || ep.TotalNudgeInsertFrames > 0)
                ? $" | nudges(drop={ep.TotalNudgeDropFrames}, insert={ep.TotalNudgeInsertFrames})"
                : "";

            return $"{ep.Name}: {conn} | {ep.Backend} | {fmt} | last={last} | fill={fillPct:0}% | errs={ep.TotalErrors} (streak {ep.ConsecutiveErrors}){nudges}";
        }

        static void PrintInputStatus(WasapiDualRouter router)
        {
            Console.WriteLine($"Ingest requested: {router.RequestedInputIngestMode}");
            Console.WriteLine($"Ingest effective: {router.EffectiveInputIngestMode}");
            if (!string.IsNullOrWhiteSpace(router.InputWarning))
                Console.WriteLine($"Warning: {router.InputWarning}");

            var (game, shaker) = router.GetInputStatus();
            Console.WriteLine(FormatEndpoint(game));
            if (shaker is not null)
                Console.WriteLine(FormatEndpoint(shaker.Value));
        }

        root.SetHandler((InvocationContext ctx) =>
        {
            try
            {
                var configPath = ctx.ParseResult.GetValueForOption(configOption) ?? "router.json";
                var listDevices = ctx.ParseResult.GetValueForOption(listDevicesOption);
                var showFormats = ctx.ParseResult.GetValueForOption(showFormatsOption);
                var ui = ctx.ParseResult.GetValueForOption(uiOption);
                var probeCmvadr = ctx.ParseResult.GetValueForOption(probeCmvadrOption);
                var validate = ctx.ParseResult.GetValueForOption(validateOption);
                var runForSec = ctx.ParseResult.GetValueForOption(runForSecondsOption);
                var statusIntervalMs = ctx.ParseResult.GetValueForOption(statusIntervalMsOption);
                var exportUiBundleDir = ctx.ParseResult.GetValueForOption(exportUiBundleOption);

                AppLog.Info($"Command handler invoked: ui={ui}, listDevices={listDevices}, configPath={configPath}");

                if (!string.IsNullOrWhiteSpace(exportUiBundleDir))
                {
                    UiBundleExporter.ExportNeonBundle(exportUiBundleDir);
                    Console.WriteLine($"Exported Neon UI bundle to: {Path.GetFullPath(exportUiBundleDir)}");
                    return;
                }

            if (listDevices)
            {
                DeviceHelper.PrintRenderDevices(showFormats);
                Console.WriteLine();
                DeviceHelper.PrintCaptureDevices(showFormats);
                return;
            }

            if (probeCmvadr)
            {
                try
                {
                    var game = CmvadrIoctlInput.ProbeFormat(VirtualAudioDriverIoctl.GameDeviceWin32Path);
                    Console.WriteLine($"CMVADR_Game:   {game.SampleRate} Hz, {game.BitsPerSample}-bit, {game.Channels} ch");

                    try
                    {
                        var shaker = CmvadrIoctlInput.ProbeFormat(VirtualAudioDriverIoctl.ShakerDeviceWin32Path);
                        Console.WriteLine($"CMVADR_Shaker: {shaker.SampleRate} Hz, {shaker.BitsPerSample}-bit, {shaker.Channels} ch");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"CMVADR_Shaker: (not available) {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CMVADR probe failed: {ex.Message}");
                }

                return;
            }

            if (ui)
            {
                AppLog.Info("Launching WinForms UI...");
                ApplicationConfiguration.Initialize();
                AppLog.Info("Creating AaaMainForm...");

                // If the app hangs before RouterMainForm ctor logs, we need to know whether
                // WinForms itself can construct a basic Form on this machine.
                AppLog.Info("Creating WinForms smoke-test Form...");
                using (var smoke = new Form())
                {
                    smoke.Text = "(smoke)";
                }
                AppLog.Info("Smoke-test Form created OK.");

                var form = new AaaMainForm(configPath);

                // If another instance is launched, bring this window to the foreground.
                singleInstance?.StartActivationServer(() =>
                {
                    try
                    {
                        if (form.IsDisposed) return;
                        form.BeginInvoke(new Action(() =>
                        {
                            if (form.WindowState == FormWindowState.Minimized)
                                form.WindowState = FormWindowState.Normal;
                            form.Show();
                            form.BringToFront();
                            form.Activate();
                        }));
                    }
                    catch
                    {
                        // ignore
                    }
                });

                AppLog.Info("AaaMainForm created; entering Application.Run...");
                Application.Run(form);
                AppLog.Info("Application.Run returned; exiting UI mode.");
                return;
            }

            var config = RouterConfig.Load(configPath);
            Console.WriteLine($"Music input:  {config.MusicInputRenderDevice}");
            Console.WriteLine($"Shaker input: {config.ShakerInputRenderDevice}");
            Console.WriteLine($"Output:       {config.OutputRenderDevice}");

            using var router = new WasapiDualRouter(config);

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                router.Stop();
            };

            router.Start();

            if (validate)
            {
                Thread.Sleep(750);
                PrintInputStatus(router);
                router.Stop();
                router.WaitUntilStopped();
                return;
            }

            var intervalMs = Math.Max(0, statusIntervalMs);
            DateTime? stopAtUtc = null;
            if (runForSec is int sec && sec > 0)
                stopAtUtc = DateTime.UtcNow.AddSeconds(sec);

            Console.WriteLine("Running. Press Ctrl+C to stop.");

            while (true)
            {
                if (stopAtUtc is not null && DateTime.UtcNow >= stopAtUtc.Value)
                {
                    router.Stop();
                    break;
                }

                if (intervalMs > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] status");
                    PrintInputStatus(router);
                    Thread.Sleep(intervalMs);
                    continue;
                }

                // status printing disabled; just block.
                router.WaitUntilStopped();
                break;
            }

            router.WaitUntilStopped();
                return;
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (ex is InvalidOperationException && ex.Message.Contains("Use --list-devices", StringComparison.OrdinalIgnoreCase))
                    Console.Error.WriteLine("Tip: run with --list-devices and copy/paste the exact device name into router.json.");

                AppLog.Warn($"CLI handler failed: {ex}");
                return;
            }
        });

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
        finally
        {
            singleInstance?.Dispose();
        }
    }

    private static void TryShowCrashDialog(string crashPath, Exception ex)
    {
        try
        {
            // Avoid throwing from crash handler; also don't block headless runs.
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            var isUi = args.Length == 0 || args.Contains("--ui", StringComparer.OrdinalIgnoreCase);
            if (!isUi)
                return;

            var where = string.IsNullOrWhiteSpace(crashPath) ? "(no crash log path available)" : crashPath;
            MessageBox.Show(
                $"Cm6206DualRouter crashed during startup.\n\nCrash log:\n{where}\n\nError:\n{ex.Message}",
                "Cm6206DualRouter crashed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // ignore
        }
    }
}
