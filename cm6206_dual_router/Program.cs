using System.CommandLine;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        AppLog.Initialize();

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

        var uiOption = new Option<bool>(
            name: "--ui",
            description: "Launch the UI");

        var root = new RootCommand("CM6206 Dual Virtual Router (2 virtual outputs -> 1 CM6206 7.1)");
        root.AddOption(configOption);
        root.AddOption(listDevicesOption);
        root.AddOption(uiOption);

        root.SetHandler((configPath, listDevices, ui) =>
        {
            AppLog.Info($"Command handler invoked: ui={ui}, listDevices={listDevices}, configPath={configPath}");
            if (listDevices)
            {
                DeviceHelper.PrintRenderDevices();
                Console.WriteLine();
                DeviceHelper.PrintCaptureDevices();
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
            Console.WriteLine("Running. Press Ctrl+C to stop.");

            router.WaitUntilStopped();
        },
        configOption, listDevicesOption, uiOption);

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
