using System.CommandLine;
using MinecraftHapticEngine.Config;
using MinecraftHapticEngine.Engine;
using MinecraftHapticEngine.Utils;

namespace MinecraftHapticEngine;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        AppLog.Initialize();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                AppLog.Crash(ex, "AppDomain.CurrentDomain.UnhandledException");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Crash(e.Exception, "TaskScheduler.UnobservedTaskException");
        };

        var configOption = new Option<string>(
            name: "--config",
            description: "Path to engine config JSON",
            getDefaultValue: () => Path.Combine("config", "engine.json"));

        var listDevicesOption = new Option<bool>(
            name: "--list-devices",
            description: "List available Windows render devices and exit");

        var telemetryWsOverride = new Option<string?>(
            name: "--ws",
            description: "Override WebSocket URL (e.g. ws://127.0.0.1:7117/)");

        var calibrateBusOption = new Option<string?>(
            name: "--calibrate",
            description: "Run calibration click on a bus (bus name)");

        var root = new RootCommand("Minecraft Haptic Engine (telemetry -> effect mapping -> real-time synthesis -> audio buses)");
        root.AddOption(configOption);
        root.AddOption(listDevicesOption);
        root.AddOption(telemetryWsOverride);
        root.AddOption(calibrateBusOption);

        root.SetHandler(async (configPath, listDevices, wsOverride, calibrateBus) =>
        {
            if (listDevices)
            {
                DeviceLister.PrintRenderDevices();
                return;
            }

            var config = JsonConfigLoader.Load(configPath);
            if (!string.IsNullOrWhiteSpace(wsOverride))
            {
                config = config with { Telemetry = config.Telemetry with { WebSocketUrl = wsOverride } };
            }

            using var engine = new HapticEngine(config);

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                engine.Stop();
            };

            engine.Start();

            if (!string.IsNullOrWhiteSpace(calibrateBus))
            {
                engine.StartCalibration(calibrateBus);
            }

            Console.WriteLine("Running. Press Ctrl+C to stop.");
            await engine.WaitForStopAsync();
        },
        configOption, listDevicesOption, telemetryWsOverride, calibrateBusOption);

        try
        {
            return await root.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            var path = AppLog.Crash(ex, "Program.Main");
            if (!string.IsNullOrWhiteSpace(path))
                Console.Error.WriteLine($"Fatal error. Crash log written: {path}");
            else
                Console.Error.WriteLine("Fatal error. (Failed to write crash log.)");
            return 1;
        }
    }
}
