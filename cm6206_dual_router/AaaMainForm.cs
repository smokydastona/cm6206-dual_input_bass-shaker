using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace Cm6206DualRouter;

internal sealed class AaaMainForm : Form
{
    private readonly string _configPath;
    private AaaMainView? _view;

    private readonly Panel _splash;
    private readonly Label _splashText;

    private RouterConfig? _config;
    private WasapiDualRouter? _router;

    public AaaMainForm(string configPath)
    {
        AppLog.Info("AaaMainForm ctor: begin");

        _configPath = configPath;
        _config = null;
        AppLog.Info("AaaMainForm ctor: config deferred to Shown");

        Text = "CM6206 Dual-Input Bass Shaker";
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = NeonTheme.BgPrimary;
        ForeColor = NeonTheme.TextPrimary;

        // Scalable layout: start near reference resolution but clamp to working area.
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1600, 900);
        var w = Math.Min(AaaUiMetrics.BaseWidth, wa.Width);
        var h = Math.Min(AaaUiMetrics.BaseHeight, wa.Height);
        ClientSize = new Size(w, h);

        // Lightweight placeholder that should never hang.
        _splashText = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = NeonTheme.TextPrimary,
            BackColor = NeonTheme.BgPrimary,
            Text = "Starting UI..."
        };
        _splash = new Panel { Dock = DockStyle.Fill, BackColor = NeonTheme.BgPrimary };
        _splash.Controls.Add(_splashText);
        Controls.Add(_splash);

        Shown += async (_, _) => await OnShownAsync();
        Resize += (_, _) => _view?.ApplyScaledLayout();

        FormClosing += (_, _) =>
        {
            StopRouter();
        };

        // Immediate startup state.
        _splashText.Text = "Status: Starting...";

        AppLog.Info("AaaMainForm ctor: end");
    }

    private async Task OnShownAsync()
    {
        try
        {
            AppLog.Info("AAA UI OnShownAsync: begin");

            _splashText.Text = "Status: Building UI...";
            await EnsureViewAsync().ConfigureAwait(true);

            _view!.BuildInfo.Text = $"Build: {typeof(Program).Assembly.GetName().Version}";
            _view.OpenLogFolder.Click += (_, _) => OpenLogFolder();
            _view.OutputDevice.SelectionChangeCommitted += (_, _) => RestartRouterFromUi();
            _view.InputA.SelectionChangeCommitted += (_, _) => RestartRouterFromUi();
            _view.InputB.SelectionChangeCommitted += (_, _) => RestartRouterFromUi();
            _view.PresetGaming.Click += (_, _) => ApplyPreset("Gaming");
            _view.PresetMovies.Click += (_, _) => ApplyPreset("Movies");
            _view.PresetMusic.Click += (_, _) => ApplyPreset("Music");
            _view.PresetCustom.Click += (_, _) => ApplyPreset("Custom");

            _view.DevicePill.State = PillState.Unknown;
            _view.DevicePill.Text = "Device: (detecting)";
            _view.StatusText.Text = "Status: Loading config...";

            // Load config off the UI thread with a timeout so UI always appears.
            _config = await LoadConfigAsync(_configPath, TimeSpan.FromSeconds(3)).ConfigureAwait(true);
            AppLog.Info("AAA UI OnShownAsync: config loaded");

            _view.StatusText.Text = "Status: Enumerating audio devices...";
            await RefreshDevicesAsync().ConfigureAwait(true);

            // Best-effort: pick config values if present.
            SelectIfPresent(_view.OutputDevice, _config.OutputRenderDevice);
            SelectIfPresent(_view.InputA, _config.MusicInputRenderDevice);
            SelectIfPresent(_view.InputB, _config.ShakerInputRenderDevice);

            RestartRouterFromUi();
            AppLog.Info("AAA UI OnShownAsync: end");
        }
        catch (Exception ex)
        {
            AppLog.Error("AAA UI OnShownAsync failed", ex);

            if (_view is null)
            {
                _splashText.Text = $"Status: {ex.Message}";
                return;
            }

            _view.DevicePill.State = PillState.Error;
            _view.DevicePill.Text = "Device: Error";
            _view.StatusText.Text = $"Status: {ex.Message}";
        }
    }

    private Task EnsureViewAsync()
    {
        if (_view is not null) return Task.CompletedTask;

        return Task.Run(() =>
        {
            // Create view on UI thread; using BeginInvoke avoids any constructor-time reentrancy issues.
            var tcs = new TaskCompletionSource();
            BeginInvoke(new Action(() =>
            {
                try
                {
                    AppLog.Info("AAA UI: creating AaaMainView...");
                    var view = new AaaMainView();
                    Controls.Remove(_splash);
                    _splash.Dispose();
                    _view = view;
                    Controls.Add(view);
                    view.BringToFront();
                    view.ApplyScaledLayout();
                    AppLog.Info("AAA UI: AaaMainView created and attached");
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }));
            tcs.Task.GetAwaiter().GetResult();
        });
    }

    private static async Task<RouterConfig> LoadConfigAsync(string path, TimeSpan timeout)
    {
        var task = Task.Run(() => RouterConfig.Load(path, validate: false));
        var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
        if (completed != task)
            throw new TimeoutException($"Timed out loading config: {path}");
        return await task.ConfigureAwait(false);
    }

    private async Task RefreshDevicesAsync()
    {
        if (_view is not null)
            _view.StatusText.Text = "Status: Enumerating audio devices...";

        var task = Task.Run(() =>
        {
            using var enumerator = new MMDeviceEnumerator();
            var render = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            // Inputs A/B are render endpoints captured via WASAPI loopback, so list render endpoints for all three.
            var names = render
                .Select(d => d.FriendlyName)
                .Append(DeviceHelper.DefaultSystemRenderDevice)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToArray();

            return names;
        });

        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(true);
        if (completed != task)
            throw new TimeoutException("Timed out while enumerating audio devices.");

        var renderNames = await task.ConfigureAwait(true);

        if (_view is not null)
        {
            _view.OutputDevice.Items.Clear();
            _view.InputA.Items.Clear();
            _view.InputB.Items.Clear();

            _view.OutputDevice.Items.AddRange(renderNames);
            _view.InputA.Items.AddRange(renderNames);
            _view.InputB.Items.AddRange(renderNames);

            _view.StatusText.Text = "Status: Devices loaded.";
        }
    }

    private void RestartRouterFromUi()
    {
        var view = _view;
        var config = _config;
        if (view is null || config is null)
            return;

        try
        {
            var output = view.OutputDevice.SelectedItem as string;
            var inputA = view.InputA.SelectedItem as string;
            var inputB = view.InputB.SelectedItem as string;

            if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(inputA) || string.IsNullOrWhiteSpace(inputB))
            {
                view.DevicePill.State = PillState.Warning;
                view.DevicePill.Text = "Device: Not configured";
                view.StatusText.Text = "Status: Select Output/Input A/Input B";
                StopRouter();
                return;
            }

            config.OutputRenderDevice = output;
            config.MusicInputRenderDevice = inputA;
            config.ShakerInputRenderDevice = inputB;
            config.Validate(requireDevices: false);

            StopRouter();
            _router = new WasapiDualRouter(config);
            _router.Start();

            view.DevicePill.State = PillState.Ok;
            view.DevicePill.Text = "Device: Connected";
            view.StatusText.Text = "Status: Running";
        }
        catch (Exception ex)
        {
            AppLog.Warn($"AAA UI failed to start router: {ex.Message}");
            view.DevicePill.State = PillState.Error;
            view.DevicePill.Text = "Device: Error";
            view.StatusText.Text = $"Status: {ex.Message}";
            StopRouter();
        }
    }

    private void StopRouter()
    {
        try
        {
            _router?.Stop();
            _router?.Dispose();
        }
        catch
        {
            // ignore
        }
        finally
        {
            _router = null;
        }
    }

    private void ApplyPreset(string preset)
    {
        // UI spec defines the preset list + interaction; exact DSP value wiring will be added next.
        var view = _view;
        if (view is null)
            return;

        view.StatusText.Text = $"Status: Preset applied: {preset}";
    }

    private static void SelectIfPresent(ComboBox combo, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (combo.Items.Contains(value))
        {
            combo.SelectedItem = value;
            return;
        }

        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is string s && s.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    private static void OpenLogFolder()
    {
        try
        {
            var dir = string.IsNullOrWhiteSpace(AppLog.LogsDirectory)
                ? Path.GetTempPath()
                : AppLog.LogsDirectory;
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLog.Warn($"OpenLogFolder failed: {ex.Message}");
        }
    }
}
