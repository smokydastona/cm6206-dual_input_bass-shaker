using System.CommandLine;

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

        var root = new RootCommand("CM6206 Dual Virtual Router (2 virtual outputs -> 1 CM6206 7.1)");
        root.AddOption(configOption);
        root.AddOption(listDevicesOption);

        root.SetHandler((configPath, listDevices) =>
        {
            if (listDevices)
            {
                DeviceHelper.PrintRenderDevices();
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
        configOption, listDevicesOption);

        return root.Invoke(args);
    }
}
