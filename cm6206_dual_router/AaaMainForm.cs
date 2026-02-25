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
    private readonly AaaMainView _view;

    private RouterConfig _config;
    private WasapiDualRouter? _router;

    public AaaMainForm(string configPath)
    {
        AppLog.Info("AaaMainForm ctor: begin");

        _configPath = configPath;
        _config = RouterConfig.Load(_configPath);

        Text = "CM6206 Dual-Input Bass Shaker";
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = NeonTheme.BgPrimary;
        ForeColor = NeonTheme.TextPrimary;

        // Scalable layout: start near reference resolution but clamp to working area.
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1600, 900);
        var w = Math.Min(AaaUiMetrics.BaseWidth, wa.Width);
        var h = Math.Min(AaaUiMetrics.BaseHeight, wa.Height);
        ClientSize = new Size(w, h);

        _view = new AaaMainView();
        Controls.Add(_view);

        _view.BuildInfo.Text = $"Build: {typeof(Program).Assembly.GetName().Version}";

        _view.OpenLogFolder.Click += (_, _) => OpenLogFolder();

        _view.OutputDevice.SelectionChangeCommitted += (_, _) => RestartRouterFromUi();
        _view.InputA.SelectionChangeCommitted += (_, _) => RestartRouterFromUi();
        _view.InputB.SelectionChangeCommitted += (_, _) => RestartRouterFromUi();

        _view.PresetGaming.Click += (_, _) => ApplyPreset("Gaming");
        _view.PresetMovies.Click += (_, _) => ApplyPreset("Movies");
        _view.PresetMusic.Click += (_, _) => ApplyPreset("Music");
        _view.PresetCustom.Click += (_, _) => ApplyPreset("Custom");

        Shown += async (_, _) => await OnShownAsync();
        Resize += (_, _) => _view.ApplyScaledLayout();

        FormClosing += (_, _) =>
        {
            StopRouter();
        };

        // Immediate startup state.
        _view.DevicePill.State = PillState.Unknown;
        _view.DevicePill.Text = "Device: (detecting)";
        _view.StatusText.Text = "Status: Starting...";

        AppLog.Info("AaaMainForm ctor: end");
    }

    private async Task OnShownAsync()
    {
        try
        {
            // Enumerate devices off the UI thread; keep UI responsive.
            await RefreshDevicesAsync().ConfigureAwait(true);

            // Best-effort: pick config values if present.
            SelectIfPresent(_view.OutputDevice, _config.OutputRenderDevice);
            SelectIfPresent(_view.InputA, _config.MusicInputRenderDevice);
            SelectIfPresent(_view.InputB, _config.ShakerInputRenderDevice);

            RestartRouterFromUi();
        }
        catch (Exception ex)
        {
            AppLog.Error("AAA UI OnShownAsync failed", ex);
            _view.DevicePill.State = PillState.Error;
            _view.DevicePill.Text = "Device: Error";
            _view.StatusText.Text = $"Status: {ex.Message}";
        }
    }

    private async Task RefreshDevicesAsync()
    {
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

        _view.OutputDevice.Items.Clear();
        _view.InputA.Items.Clear();
        _view.InputB.Items.Clear();

        _view.OutputDevice.Items.AddRange(renderNames);
        _view.InputA.Items.AddRange(renderNames);
        _view.InputB.Items.AddRange(renderNames);

        _view.StatusText.Text = "Status: Devices loaded.";
    }

    private void RestartRouterFromUi()
    {
        try
        {
            var output = _view.OutputDevice.SelectedItem as string;
            var inputA = _view.InputA.SelectedItem as string;
            var inputB = _view.InputB.SelectedItem as string;

            if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(inputA) || string.IsNullOrWhiteSpace(inputB))
            {
                _view.DevicePill.State = PillState.Warning;
                _view.DevicePill.Text = "Device: Not configured";
                _view.StatusText.Text = "Status: Select Output/Input A/Input B";
                StopRouter();
                return;
            }

            _config.OutputRenderDevice = output;
            _config.MusicInputRenderDevice = inputA;
            _config.ShakerInputRenderDevice = inputB;
            _config.Validate();

            StopRouter();
            _router = new WasapiDualRouter(_config);
            _router.Start();

            _view.DevicePill.State = PillState.Ok;
            _view.DevicePill.Text = "Device: Connected";
            _view.StatusText.Text = "Status: Running";
        }
        catch (Exception ex)
        {
            AppLog.Warn($"AAA UI failed to start router: {ex.Message}");
            _view.DevicePill.State = PillState.Error;
            _view.DevicePill.Text = "Device: Error";
            _view.StatusText.Text = $"Status: {ex.Message}";
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
        _view.StatusText.Text = $"Status: Preset applied: {preset}";
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
