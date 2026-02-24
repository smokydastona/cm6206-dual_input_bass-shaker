using System.CommandLine;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal static class Program
{
    public static int Main(string[] args)
    {
        var configOption = new Option<string>(
            name: "--config",
            description: "Path to router.json",
            getDefaultValue: () => "router.json");

        var listDevicesOption = new Option<bool>(
            name: "--list-devices",
            description: "List available playback devices and exit");

        var uiOption = new Option<bool>(
            name: "--ui",
            description: "Launch the tabbed UI (device selection + per-channel gains)");

        var root = new RootCommand("CM6206 Dual Virtual Router (2 virtual outputs -> 1 CM6206 7.1)");
        root.AddOption(configOption);
        root.AddOption(listDevicesOption);
        root.AddOption(uiOption);

        root.SetHandler((configPath, listDevices, ui) =>
        {
            if (listDevices)
            {
                DeviceHelper.PrintRenderDevices();
                return;
            }

            if (ui)
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new RouterMainForm(configPath));
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

        return root.Invoke(args);
    }
}
