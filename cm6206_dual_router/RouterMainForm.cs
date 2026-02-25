using System.Text.Json;
using System.Text;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.IO;

namespace Cm6206DualRouter;

public sealed class RouterMainForm : Form
{
    private readonly string _configPath;

    private readonly ComboBox _profileCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _profileLoadButton = new() { Text = "Load" };
    private readonly Button _profileSaveAsButton = new() { Text = "Save As" };
    private readonly Button _profileDeleteButton = new() { Text = "Delete" };
    private readonly Button _profileImportButton = new() { Text = "Import..." };
    private readonly Button _profileOpenFolderButton = new() { Text = "Open folder" };
    private readonly CheckBox _profileAutoSwitch = new() { Text = "Auto-switch by running apps", AutoSize = true };
    private readonly NumericUpDown _profilePollMs = new() { Minimum = 250, Maximum = 5000, DecimalPlaces = 0, Increment = 250, Value = 1000 };
    private readonly System.Windows.Forms.Timer _profilePollTimer = new();

    private readonly System.Windows.Forms.Timer _metersTimer = new();
    private readonly ProgressBar[] _musicMeters = new ProgressBar[2];
    private readonly Label[] _musicMeterLabels = new Label[2];
    private readonly Label[] _musicClipLabels = new Label[2];

    private readonly ProgressBar[] _shakerMeters = new ProgressBar[2];
    private readonly Label[] _shakerMeterLabels = new Label[2];
    private readonly Label[] _shakerClipLabels = new Label[2];

    private readonly ProgressBar[] _outputMeters = new ProgressBar[8];
    private readonly Label[] _outputMeterLabels = new Label[8];
    private readonly Label[] _outputClipLabels = new Label[8];

    private readonly float[] _meterTmpMusic = new float[2];
    private readonly float[] _meterTmpShaker = new float[2];
    private readonly float[] _meterTmpOut = new float[8];

    private readonly float[] _displayMusic = new float[2];
    private readonly float[] _displayShaker = new float[2];
    private readonly float[] _displayOut = new float[8];

    private string? _lastAutoProfileApplied;

    private readonly ComboBox _musicDeviceCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _shakerDeviceCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _outputDeviceCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly ComboBox _latencyInputCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _measureLatencyButton = new() { Text = "Measure latency" };
    private readonly Label _latencyResultLabel = new() { Text = "", AutoSize = true };

    private readonly NumericUpDown _musicGainDb = new() { Minimum = -60, Maximum = 20, DecimalPlaces = 1, Increment = 0.5M };
    private readonly NumericUpDown _shakerGainDb = new() { Minimum = -60, Maximum = 20, DecimalPlaces = 1, Increment = 0.5M };

    private readonly NumericUpDown _hpHz = new() { Minimum = 1, Maximum = 300, DecimalPlaces = 1, Increment = 1 };
    private readonly NumericUpDown _lpHz = new() { Minimum = 5, Maximum = 300, DecimalPlaces = 1, Increment = 1 };

    private readonly ComboBox _mixingModeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly NumericUpDown _latencyMs = new() { Minimum = 10, Maximum = 500, DecimalPlaces = 0, Increment = 5 };

    private readonly ComboBox _sampleRateCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly CheckBox _useCenter = new() { Text = "Use Center channel" };
    private readonly CheckBox _useExclusiveMode = new() { Text = "Use WASAPI Exclusive mode (if supported)" };

    private readonly Label _mixFormatLabel = new() { Text = "", AutoSize = true };
    private readonly Label _effectiveFormatLabel = new() { Text = "", AutoSize = true };
    private readonly Label _formatWarningLabel = new() { Text = "", AutoSize = true };
    private readonly Button _probeFormatsButton = new() { Text = "Probe exclusive formats" };
    private readonly Button _toggleBlacklistButton = new() { Text = "Toggle blacklist" };
    private readonly ListBox _formatList = new() { IntegralHeight = false, Height = 130 };

    private readonly Button _diagnosticsRefreshButton = new() { Text = "Refresh" };
    private readonly Button _diagnosticsOpenSoundSettingsButton = new() { Text = "Open Sound settings" };
    private readonly Button _diagnosticsOpenClassicSoundButton = new() { Text = "Open Sound control panel" };
    private readonly Button _diagnosticsLaunchVendorButton = new() { Text = "Launch C-Media control panel" };
    private readonly Label _diagnosticsVendorStatusLabel = new() { Text = "", AutoSize = true };

    private readonly Button _cm6206HidScanButton = new() { Text = "Scan CM6206 HID" };
    private readonly ComboBox _cm6206HidDeviceCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly NumericUpDown _cm6206HidRegCount = new() { Minimum = 1, Maximum = 64, DecimalPlaces = 0, Increment = 1, Value = 6, Width = 60 };
    private readonly Button _cm6206HidReadRegsButton = new() { Text = "Read regs" };
    private readonly Label _cm6206HidStatusLabel = new() { Text = "", AutoSize = true };

    private string? _cm6206HidLastDump;
    private readonly TextBox _diagnosticsText = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        WordWrap = false
    };

    private readonly TrackBar[] _channelSliders = new TrackBar[8];
    private readonly Label[] _channelLabels = new Label[8];
    private readonly CheckBox[] _channelMute = new CheckBox[8];
    private readonly CheckBox[] _channelInvert = new CheckBox[8];
    private readonly CheckBox[] _channelSolo = new CheckBox[8];
    private readonly ComboBox[] _channelMap = new ComboBox[8];
    private readonly Button[] _visualMapButtons = new Button[8];

    private readonly Button _identityMapButton = new() { Text = "Identity map" };
    private readonly Button _swapSideRearButton = new() { Text = "Swap Side/Rear" };

    private readonly Button _refreshButton = new() { Text = "Refresh devices" };
    private readonly Button _saveButton = new() { Text = "Save config" };
    private readonly Button _startButton = new() { Text = "Start" };
    private readonly Button _stopButton = new() { Text = "Stop", Enabled = false };

    private RouterConfig _config;
    private WasapiDualRouter? _router;
    private TonePlayer? _tonePlayer;

    private readonly ComboBox _testChannelCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _testTypeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _testFreq = new() { Minimum = 10, Maximum = 20000, DecimalPlaces = 0, Increment = 10 };
    private readonly NumericUpDown _testFreqEnd = new() { Minimum = 10, Maximum = 20000, DecimalPlaces = 0, Increment = 10 };
    private readonly NumericUpDown _testSweepSec = new() { Minimum = 1, Maximum = 60, DecimalPlaces = 1, Increment = 0.5M };
    private readonly NumericUpDown _testLevelDb = new() { Minimum = -60, Maximum = 0, DecimalPlaces = 1, Increment = 1 };
    private readonly ComboBox _testPresetCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _testVoicePrompts = new() { Text = "Voice prompts", AutoSize = true };
    private readonly CheckBox _testAutoStep = new() { Text = "Auto-step channels", AutoSize = true };
    private readonly CheckBox _testLoop = new() { Text = "Loop", AutoSize = true };
    private readonly NumericUpDown _testStepMs = new() { Minimum = 250, Maximum = 30000, DecimalPlaces = 0, Increment = 250 };
    private readonly Button _testStartButton = new() { Text = "Start test" };
    private readonly Button _testStopButton = new() { Text = "Stop test", Enabled = false };

    private readonly System.Windows.Forms.Timer _autoStepTimer = new();
    private bool _alternateIsSine = true;

    private static readonly string[] ChannelNames =
    [
        "Front Left (FL)",
        "Front Right (FR)",
        "Center (FC)",
        "LFE",
        "Back Left (BL)",
        "Back Right (BR)",
        "Side Left (SL)",
        "Side Right (SR)"
    ];

    public RouterMainForm(string configPath)
    {
        _configPath = configPath;
        Text = "CM6206 Dual Router";
        Width = 860;
        Height = 620;
        StartPosition = FormStartPosition.CenterScreen;

        _config = LoadOrCreateConfigForUi(_configPath);

        var tabs = new TabControl { Dock = DockStyle.Fill };

        tabs.TabPages.Add(BuildDevicesTab());
        tabs.TabPages.Add(BuildDiagnosticsTab());
        tabs.TabPages.Add(BuildDspTab());
        tabs.TabPages.Add(BuildChannelsTab());
        tabs.TabPages.Add(BuildMetersTab());
        tabs.TabPages.Add(BuildCalibrationTab());

        Controls.Add(tabs);

        _autoStepTimer.Tick += (_, _) => AutoStepTick();
        _profilePollTimer.Tick += (_, _) => AutoProfileSwitchTick();
        _metersTimer.Interval = 50;
        _metersTimer.Tick += (_, _) => UpdateMetersTick();

        FormClosing += (_, _) =>
        {
            StopTest();
            StopRouter();
            VoicePrompter.Dispose();
        };

        RefreshDeviceLists();
        LoadConfigIntoControls();

        WireFormatUi();
        UpdateFormatInfo();

        RefreshProfilesCombo();

        UpdateDiagnostics();
    }

    private TabPage BuildMetersTab()
    {
        var page = new TabPage("Meters");

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(new Label
        {
            Text = "Peak meters (dBFS). Start the router to see levels.",
            AutoSize = true
        }, 0, 0);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            RowCount = 1,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); // name
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // bar
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));  // dB label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));  // clip

        var row = 0;
        layout.Controls.Add(new Label { Text = "Signal", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, row);
        layout.Controls.Add(new Label { Text = "Level", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 1, row);
        layout.Controls.Add(new Label { Text = "dBFS", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 2, row);
        layout.Controls.Add(new Label { Text = "", AutoSize = true }, 3, row);
        row++;

        AddMeterRow(layout, ref row, "Music L", _musicMeters, _musicMeterLabels, _musicClipLabels, 0);
        AddMeterRow(layout, ref row, "Music R", _musicMeters, _musicMeterLabels, _musicClipLabels, 1);
        AddMeterRow(layout, ref row, "Shaker L", _shakerMeters, _shakerMeterLabels, _shakerClipLabels, 0);
        AddMeterRow(layout, ref row, "Shaker R", _shakerMeters, _shakerMeterLabels, _shakerClipLabels, 1);

        for (var ch = 0; ch < 8; ch++)
            AddMeterRow(layout, ref row, $"Out {ShortName(ch)}", _outputMeters, _outputMeterLabels, _outputClipLabels, ch);

        scroll.Controls.Add(layout);
        root.Controls.Add(scroll, 0, 1);

        page.Controls.Add(root);
        return page;
    }

    private static void AddMeterRow(
        TableLayoutPanel layout,
        ref int row,
        string name,
        ProgressBar[] bars,
        Label[] labels,
        Label[] clipLabels,
        int index)
    {
        var bar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 1000,
            Value = 0,
            Style = ProgressBarStyle.Continuous
        };
        var label = new Label { Text = "-inf", AutoSize = true };
        var clip = new Label { Text = "", AutoSize = true };

        bars[index] = bar;
        labels[index] = label;
        clipLabels[index] = clip;

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.Controls.Add(new Label { Text = name, AutoSize = true }, 0, row);
        layout.Controls.Add(bar, 1, row);
        layout.Controls.Add(label, 2, row);
        layout.Controls.Add(clip, 3, row);
        row++;
    }

    private void UpdateMetersTick()
    {
        if (_router is null)
        {
            ClearMeters();
            return;
        }

        _router.CopyPeakValues(_meterTmpMusic, _meterTmpShaker, _meterTmpOut);

        const float release = 0.85f;
        for (var i = 0; i < 2; i++)
        {
            _displayMusic[i] = Math.Max(_displayMusic[i] * release, SafeAbs(_meterTmpMusic[i]));
            _displayShaker[i] = Math.Max(_displayShaker[i] * release, SafeAbs(_meterTmpShaker[i]));
        }
        for (var i = 0; i < 8; i++)
            _displayOut[i] = Math.Max(_displayOut[i] * release, SafeAbs(_meterTmpOut[i]));

        UpdateMeterUi(_musicMeters, _musicMeterLabels, _musicClipLabels, _displayMusic);
        UpdateMeterUi(_shakerMeters, _shakerMeterLabels, _shakerClipLabels, _displayShaker);
        UpdateMeterUi(_outputMeters, _outputMeterLabels, _outputClipLabels, _displayOut);
    }

    private void ClearMeters()
    {
        Array.Clear(_displayMusic);
        Array.Clear(_displayShaker);
        Array.Clear(_displayOut);

        UpdateMeterUi(_musicMeters, _musicMeterLabels, _musicClipLabels, _displayMusic);
        UpdateMeterUi(_shakerMeters, _shakerMeterLabels, _shakerClipLabels, _displayShaker);
        UpdateMeterUi(_outputMeters, _outputMeterLabels, _outputClipLabels, _displayOut);
    }

    private static void UpdateMeterUi(ProgressBar[] bars, Label[] labels, Label[] clipLabels, float[] peaks)
    {
        for (var i = 0; i < peaks.Length; i++)
        {
            var p = Math.Clamp(peaks[i], 0f, 1f);
            var v = (int)Math.Round(p * 1000);
            v = Math.Clamp(v, bars[i].Minimum, bars[i].Maximum);
            bars[i].Value = v;
            labels[i].Text = FormatDbfs(p);
            clipLabels[i].Text = p >= 0.999f ? "CLIP" : "";
        }
    }

    private static string FormatDbfs(float peak)
    {
        if (peak <= 0f)
            return "-inf";

        var db = 20.0 * Math.Log10(peak);
        if (db < -99) db = -99;
        return $"{db:0.0}";
    }

    private static float SafeAbs(float x) => x < 0 ? -x : x;

    private void WireFormatUi()
    {
        _outputDeviceCombo.SelectedIndexChanged += (_, _) => UpdateFormatInfo();
        _useExclusiveMode.CheckedChanged += (_, _) => UpdateFormatInfo();
        _sampleRateCombo.SelectedIndexChanged += (_, _) => UpdateFormatInfo();

        _probeFormatsButton.Click += (_, _) => ProbeExclusiveFormats();
        _toggleBlacklistButton.Click += (_, _) => ToggleBlacklistForSelectedRate();
    }

    private TabPage BuildDevicesTab()
    {
        var page = new TabPage("Devices");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            Padding = new Padding(12),
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Music input device", AutoSize = true }, 0, 0);
        layout.Controls.Add(_musicDeviceCombo, 1, 0);

        layout.Controls.Add(new Label { Text = "Shaker input device", AutoSize = true }, 0, 1);
        layout.Controls.Add(_shakerDeviceCombo, 1, 1);

        layout.Controls.Add(new Label { Text = "Output device (CM6206)", AutoSize = true }, 0, 2);
        layout.Controls.Add(_outputDeviceCombo, 1, 2);

        layout.Controls.Add(new Label { Text = "Latency input (mic/line-in)", AutoSize = true }, 0, 3);
        var latencyRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        _latencyInputCombo.Width = 360;
        latencyRow.Controls.Add(_latencyInputCombo);
        latencyRow.Controls.Add(_measureLatencyButton);
        latencyRow.Controls.Add(_latencyResultLabel);
        layout.Controls.Add(latencyRow, 1, 3);

        layout.Controls.Add(new Label { Text = "Profile", AutoSize = true }, 0, 4);
        var profileRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        _profileCombo.Width = 240;
        profileRow.Controls.Add(_profileCombo);
        profileRow.Controls.Add(_profileLoadButton);
        profileRow.Controls.Add(_profileSaveAsButton);
        profileRow.Controls.Add(_profileDeleteButton);
        profileRow.Controls.Add(_profileImportButton);
        profileRow.Controls.Add(_profileOpenFolderButton);
        profileRow.Controls.Add(_profileAutoSwitch);
        profileRow.Controls.Add(new Label { Text = "Poll (ms)", AutoSize = true, Padding = new Padding(6, 6, 0, 0) });
        _profilePollMs.Width = 80;
        profileRow.Controls.Add(_profilePollMs);
        layout.Controls.Add(profileRow, 1, 4);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        buttons.Controls.Add(_refreshButton);
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(_startButton);
        buttons.Controls.Add(_stopButton);

        layout.Controls.Add(buttons, 1, 5);

        _refreshButton.Click += (_, _) => RefreshDeviceLists();
        _saveButton.Click += (_, _) =>
        {
            try
            {
                SaveConfigFromControls();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        _startButton.Click += (_, _) => StartRouter();
        _stopButton.Click += (_, _) => StopRouter();

        _profileLoadButton.Click += (_, _) => LoadSelectedProfileIntoUi();
        _profileSaveAsButton.Click += (_, _) => SaveCurrentAsNewProfile();
        _profileDeleteButton.Click += (_, _) => DeleteSelectedProfile();
        _profileImportButton.Click += (_, _) => ImportProfileFile();
        _profileOpenFolderButton.Click += (_, _) => OpenProfilesFolder();
        _profileAutoSwitch.CheckedChanged += (_, _) => UpdateAutoProfileTimer();
        _profilePollMs.ValueChanged += (_, _) => UpdateAutoProfileTimer();

        _measureLatencyButton.Click += async (_, _) => await MeasureLatencyAsync();

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildDiagnosticsTab()
    {
        var page = new TabPage("Diagnostics");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = true
        };

        buttons.Controls.Add(_diagnosticsRefreshButton);
        buttons.Controls.Add(_diagnosticsOpenSoundSettingsButton);
        buttons.Controls.Add(_diagnosticsOpenClassicSoundButton);
        buttons.Controls.Add(_diagnosticsLaunchVendorButton);
        buttons.Controls.Add(_diagnosticsVendorStatusLabel);

        buttons.Controls.Add(new Label { Text = "   ", AutoSize = true });
        buttons.Controls.Add(_cm6206HidScanButton);
        buttons.Controls.Add(_cm6206HidDeviceCombo);
        buttons.Controls.Add(new Label { Text = "Regs:", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(0, 6, 0, 0) });
        buttons.Controls.Add(_cm6206HidRegCount);
        buttons.Controls.Add(_cm6206HidReadRegsButton);
        buttons.Controls.Add(_cm6206HidStatusLabel);

        _diagnosticsRefreshButton.Click += (_, _) => UpdateDiagnostics();
        _diagnosticsOpenSoundSettingsButton.Click += (_, _) =>
            Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true });
        _diagnosticsOpenClassicSoundButton.Click += (_, _) =>
            Process.Start(new ProcessStartInfo("control.exe", "mmsys.cpl") { UseShellExecute = true });

        _diagnosticsLaunchVendorButton.Click += (_, _) =>
        {
            if (VendorControlPanel.TryLaunch(out var message))
            {
                _diagnosticsVendorStatusLabel.Text = message;
                return;
            }

            MessageBox.Show(this, message, "Control panel not found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _diagnosticsVendorStatusLabel.Text = message;
        };

        _cm6206HidScanButton.Click += (_, _) =>
        {
            try
            {
                _cm6206HidLastDump = null;
                _cm6206HidStatusLabel.Text = "";

                var devices = Cm6206HidClient.EnumerateDevices();
                _cm6206HidDeviceCombo.BeginUpdate();
                _cm6206HidDeviceCombo.Items.Clear();
                _cm6206HidDeviceCombo.DisplayMember = nameof(HidComboItem.Display);
                _cm6206HidDeviceCombo.ValueMember = nameof(HidComboItem.DevicePath);
                foreach (var d in devices)
                {
                    var display = $"{(string.IsNullOrWhiteSpace(d.Product) ? "CM6206" : d.Product)}";
                    if (!string.IsNullOrWhiteSpace(d.SerialNumber))
                        display += $" SN={d.SerialNumber}";
                    _cm6206HidDeviceCombo.Items.Add(new HidComboItem(display, d.DevicePath));
                }
                _cm6206HidDeviceCombo.EndUpdate();

                if (_cm6206HidDeviceCombo.Items.Count > 0)
                    _cm6206HidDeviceCombo.SelectedIndex = 0;

                _cm6206HidStatusLabel.Text = $"Found: {_cm6206HidDeviceCombo.Items.Count}";
                UpdateDiagnostics();
            }
            catch (Exception ex)
            {
                _cm6206HidStatusLabel.Text = $"Error: {ex.Message}";
                _cm6206HidLastDump = null;
                UpdateDiagnostics();
            }
        };

        _cm6206HidReadRegsButton.Click += async (_, _) =>
        {
            if (_cm6206HidDeviceCombo.SelectedItem is not HidComboItem selected)
            {
                _cm6206HidStatusLabel.Text = "Select a HID device";
                return;
            }

            _cm6206HidReadRegsButton.Enabled = false;
            _cm6206HidStatusLabel.Text = "Reading...";

            try
            {
                var count = (int)_cm6206HidRegCount.Value;
                var regs = await Task.Run(() => Cm6206HidClient.ReadRegisterBlock(selected.DevicePath, count));

                var sb = new StringBuilder();
                sb.AppendLine($"Device: {selected.Display}");
                sb.AppendLine($"VID:PID = {Cm6206HidClient.VendorId:X4}:{Cm6206HidClient.ProductId:X4}");
                sb.AppendLine($"Registers (read-only):");
                foreach (var kvp in regs.OrderBy(k => k.Key))
                {
                    sb.AppendLine($"  R{kvp.Key:D2} = 0x{kvp.Value:X4} ({kvp.Value})");
                }

                _cm6206HidLastDump = sb.ToString().TrimEnd();
                _cm6206HidStatusLabel.Text = "OK";
                UpdateDiagnostics();
            }
            catch (Exception ex)
            {
                _cm6206HidLastDump = $"HID read failed: {ex.Message}";
                _cm6206HidStatusLabel.Text = "Failed";
                UpdateDiagnostics();
            }
            finally
            {
                _cm6206HidReadRegsButton.Enabled = true;
            }
        };

        layout.Controls.Add(buttons, 0, 0);
        layout.Controls.Add(_diagnosticsText, 0, 1);

        page.Controls.Add(layout);
        return page;
    }

    private void UpdateDiagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"App: CM6206 Dual Router");
        sb.AppendLine($"Config: {_configPath}");
        sb.AppendLine($"Router running: {_router is not null}");
        if (_router is not null)
            sb.AppendLine($"Effective output: 7.1 float @ {_router.EffectiveSampleRate} Hz ({(_config.UseExclusiveMode ? "Exclusive" : "Shared")})");
        if (!string.IsNullOrWhiteSpace(_router?.FormatWarning))
            sb.AppendLine($"Format warning: {_router.FormatWarning}");

        sb.AppendLine();
        sb.AppendLine("Selected devices:");
        sb.AppendLine(DescribeRenderDevice(_musicDeviceCombo.SelectedItem as string, "Music input (loopback)"));
        sb.AppendLine(DescribeRenderDevice(_shakerDeviceCombo.SelectedItem as string, "Shaker input (loopback)"));
        sb.AppendLine(DescribeRenderDevice(_outputDeviceCombo.SelectedItem as string, "Output"));
        sb.AppendLine(DescribeCaptureDevice(_latencyInputCombo.SelectedItem as string, "Latency input (capture)"));

        sb.AppendLine();
        sb.AppendLine("Vendor control panel:");
        if (VendorControlPanel.TryFindExecutable(out var exePath, out var reason))
            sb.AppendLine($"Found: {exePath}");
        else
            sb.AppendLine($"Not found: {reason}");

        sb.AppendLine();
        sb.AppendLine("CM6206 HID probe (read-only):");
        sb.AppendLine($"Target VID:PID = {Cm6206HidClient.VendorId:X4}:{Cm6206HidClient.ProductId:X4}");
        if (_cm6206HidDeviceCombo.Items.Count == 0)
        {
            sb.AppendLine("Devices: (scan not run yet, or none found)");
        }
        else
        {
            sb.AppendLine($"Devices: {_cm6206HidDeviceCombo.Items.Count}");
            for (var i = 0; i < _cm6206HidDeviceCombo.Items.Count; i++)
            {
                if (_cm6206HidDeviceCombo.Items[i] is HidComboItem item)
                {
                    var marker = i == _cm6206HidDeviceCombo.SelectedIndex ? "*" : " ";
                    sb.AppendLine($"{marker} {item.Display}");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_cm6206HidLastDump))
        {
            sb.AppendLine();
            sb.AppendLine(_cm6206HidLastDump);
        }

        _diagnosticsText.Text = sb.ToString();
    }

    private sealed record HidComboItem(string Display, string DevicePath);

    private static string DescribeRenderDevice(string? friendlyName, string label)
    {
        if (string.IsNullOrWhiteSpace(friendlyName))
            return $"- {label}: (not selected)";

        try
        {
            using var device = DeviceHelper.GetRenderDeviceByFriendlyName(friendlyName);
            return DescribeDeviceCommon(device, label);
        }
        catch (Exception ex)
        {
            return $"- {label}: {friendlyName} (error: {ex.Message})";
        }
    }

    private static string DescribeCaptureDevice(string? friendlyName, string label)
    {
        if (string.IsNullOrWhiteSpace(friendlyName))
            return $"- {label}: (not selected)";

        try
        {
            using var device = DeviceHelper.GetCaptureDeviceByFriendlyName(friendlyName);
            return DescribeDeviceCommon(device, label);
        }
        catch (Exception ex)
        {
            return $"- {label}: {friendlyName} (error: {ex.Message})";
        }
    }

    private static string DescribeDeviceCommon(MMDevice device, string label)
    {
        var mix = device.AudioClient.MixFormat;
        var channels = mix.Channels;
        var rate = mix.SampleRate;
        var bits = mix.BitsPerSample;
        var encoding = mix.Encoding.ToString();

        var extra = "";
        // NAudio API differs across versions; use reflection if we can.
        var mask = TryGetChannelMask(mix);
        if (mask is not null)
            extra = $", mask=0x{mask.Value:X}";

        return $"- {label}: {device.FriendlyName}\r\n    State={device.State}, MixFormat={channels}ch {rate}Hz {bits}bit {encoding}{extra}";
    }

    private static int? TryGetChannelMask(NAudio.Wave.WaveFormat waveFormat)
    {
        var t = waveFormat.GetType();
        var prop = t.GetProperty("ChannelMask");
        if (prop?.PropertyType == typeof(int))
        {
            try { return (int?)prop.GetValue(waveFormat); } catch { return null; }
        }

        var field = t.GetField("dwChannelMask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (field?.FieldType == typeof(int))
        {
            try { return (int?)field.GetValue(waveFormat); } catch { return null; }
        }

        return null;
    }

    private static RouterConfig LoadOrCreateConfigForUi(string path)
    {
        try
        {
            // UI should be able to open even if device names don't match this machine.
            return RouterConfig.Load(path, validate: false);
        }
        catch (FileNotFoundException)
        {
            var config = new RouterConfig();
            try
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(path, JsonSerializer.Serialize(config, options));
            }
            catch
            {
                // If we can't write a template, still let the UI open with defaults.
            }
            return config;
        }
    }

    private void UpdateAutoProfileTimer()
    {
        _profilePollTimer.Stop();
        _profilePollTimer.Interval = (int)_profilePollMs.Value;

        if (_profileAutoSwitch.Checked)
        {
            _lastAutoProfileApplied = null;
            _profilePollTimer.Start();
            AutoProfileSwitchTick();
        }
    }

    private void AutoProfileSwitchTick()
    {
        if (!_profileAutoSwitch.Checked) return;

        var profiles = ProfileStore.LoadAll();
        if (profiles.Count == 0) return;

        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(p.ProcessName)) continue;
                    running.Add(p.ProcessName + ".exe");
                }
                catch
                {
                    // ignore processes we can't inspect
                }
            }
        }
        catch
        {
            return;
        }

        var match = profiles.FirstOrDefault(pr =>
            pr.ProcessNames is { Length: > 0 } && pr.ProcessNames.Any(running.Contains));
        if (match is null) return;

        if (string.Equals(_lastAutoProfileApplied, match.Name, StringComparison.OrdinalIgnoreCase))
            return;

        _lastAutoProfileApplied = match.Name;

        try
        {
            // Apply profile and hot-switch router if running.
            var wasRunning = _router is not null;
            StopTest();
            StopRouter();

            ApplyProfileConfigToUi(match);

            if (wasRunning)
                StartRouter();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Auto-switch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task MeasureLatencyAsync()
    {
        try
        {
            StopTest();
            StopRouter();

            SaveConfigFromControls(showSavedDialog: false);
            _config = RouterConfig.Load(_configPath);

            var captureName = _latencyInputCombo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(captureName))
            {
                MessageBox.Show(this, "Pick a capture device (mic/line-in) first.", "Latency", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _measureLatencyButton.Enabled = false;
            _latencyResultLabel.Text = "measuring...";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var result = await LatencyMeasurer.MeasureAsync(_config, captureName, cts.Token);

            _latencyResultLabel.Text = $"~{result.EstimatedMs:0} ms";
        }
        catch (Exception ex)
        {
            _latencyResultLabel.Text = "";
            MessageBox.Show(this, ex.Message, "Latency measurement failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _measureLatencyButton.Enabled = true;
        }
    }

    private void RefreshProfilesCombo()
    {
        var profiles = ProfileStore.LoadAll();
        var selected = _profileCombo.SelectedItem as string;

        _profileCombo.Items.Clear();
        foreach (var p in profiles)
            _profileCombo.Items.Add(p.Name);

        if (!string.IsNullOrWhiteSpace(selected) && _profileCombo.Items.Contains(selected))
            _profileCombo.SelectedItem = selected;
        else if (_profileCombo.Items.Count > 0)
            _profileCombo.SelectedIndex = 0;
    }

    private void LoadSelectedProfileIntoUi()
    {
        if (_profileCombo.SelectedItem is not string name || string.IsNullOrWhiteSpace(name))
            return;

        var profile = ProfileStore.LoadAll().FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (profile is null) return;

        ApplyProfileConfigToUi(profile);
    }

    private void ApplyProfileConfigToUi(RouterProfile profile)
    {
        // Apply ONLY the allowed scope (not devices).
        // Keep existing devices + latency input capture selection.
        var keepMusic = _config.MusicInputRenderDevice;
        var keepShaker = _config.ShakerInputRenderDevice;
        var keepOutput = _config.OutputRenderDevice;
        var keepLatencyCapture = _config.LatencyInputCaptureDevice;

        _config = profile.Config;

        _config.MusicInputRenderDevice = keepMusic;
        _config.ShakerInputRenderDevice = keepShaker;
        _config.OutputRenderDevice = keepOutput;
        _config.LatencyInputCaptureDevice = keepLatencyCapture;

        // Profiles should be applicable even before device names are set up.
        _config.Validate(requireDevices: false);
        LoadConfigIntoControls();
        SaveConfigToDisk(showSavedDialog: false);
    }

    private void SaveCurrentAsNewProfile()
    {
        SaveConfigFromControls(showSavedDialog: false);

        var name = PromptDialog.Show("Save Profile", "Profile name:", "Default");
        if (name is null) return;

        var procText = PromptDialog.Show(
            "Save Profile",
            "Optional: EXE names to auto-switch on (comma-separated), e.g. game.exe, vlc.exe",
            "");

        var processNames = (procText ?? string.Empty)
            .Split([',', ';', '\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim());

        ProfileStore.Upsert(name, _config, processNames);
        RefreshProfilesCombo();
        _profileCombo.SelectedItem = name;
    }

    private void DeleteSelectedProfile()
    {
        if (_profileCombo.SelectedItem is not string name || string.IsNullOrWhiteSpace(name))
            return;

        var ok = MessageBox.Show(
            this,
            $"Delete profile '{name}'?",
            "Delete Profile",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (ok != DialogResult.Yes) return;

        ProfileStore.Delete(name);
        RefreshProfilesCombo();
    }

    private void OpenProfilesFolder()
    {
        try
        {
            var dir = ProfileStore.GetProfilesDirectory();
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Open folder failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportProfileFile()
    {
        try
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Profile JSON (*.json)|*.json|All files (*.*)|*.*",
                Title = "Import profile JSON"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var json = File.ReadAllText(dialog.FileName);
            var profile = JsonSerializer.Deserialize<RouterProfile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (profile is null || string.IsNullOrWhiteSpace(profile.Name))
                throw new InvalidOperationException("Invalid profile file (missing name).");

            ProfileStore.SaveProfile(profile);
            RefreshProfilesCombo();
            _profileCombo.SelectedItem = profile.Name;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private TabPage BuildDspTab()
    {
        var page = new TabPage("DSP");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 15,
            Padding = new Padding(12),
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Music gain (dB)", AutoSize = true }, 0, 0);
        layout.Controls.Add(_musicGainDb, 1, 0);

        layout.Controls.Add(new Label { Text = "Shaker gain (dB)", AutoSize = true }, 0, 1);
        layout.Controls.Add(_shakerGainDb, 1, 1);

        layout.Controls.Add(new Label { Text = "Shaker high-pass (Hz)", AutoSize = true }, 0, 2);
        layout.Controls.Add(_hpHz, 1, 2);

        layout.Controls.Add(new Label { Text = "Shaker low-pass (Hz)", AutoSize = true }, 0, 3);
        layout.Controls.Add(_lpHz, 1, 3);

        layout.Controls.Add(new Label { Text = "Mixing mode", AutoSize = true }, 0, 4);
        _mixingModeCombo.Width = 360;
        _mixingModeCombo.Items.Clear();
        _mixingModeCombo.Items.Add("Front = Music + Shaker (default)");
        _mixingModeCombo.Items.Add("Dedicated (Front = Music only; Shaker = Rear/Side/LFE)");
        layout.Controls.Add(_mixingModeCombo, 1, 4);

        layout.Controls.Add(new Label { Text = "Latency (ms)", AutoSize = true }, 0, 5);
        layout.Controls.Add(_latencyMs, 1, 5);

        layout.Controls.Add(new Label { Text = "Preferred sample rate (Hz)", AutoSize = true }, 0, 6);
        _sampleRateCombo.Width = 140;
        _sampleRateCombo.Items.Clear();
        foreach (var sr in OutputFormatNegotiator.CandidateSampleRates)
            _sampleRateCombo.Items.Add(sr);
        layout.Controls.Add(_sampleRateCombo, 1, 6);

        layout.Controls.Add(_useCenter, 1, 7);

        layout.Controls.Add(_useExclusiveMode, 1, 8);

        layout.Controls.Add(new Label { Text = "Output format helper", AutoSize = true }, 0, 9);

        var helperGroup = new GroupBox { Text = "Probe / warnings", Dock = DockStyle.Fill };
        var helperLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            AutoSize = true
        };
        helperLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        helperLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        helperLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        helperLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        helperLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        helperLayout.Controls.Add(_mixFormatLabel, 0, 0);
        helperLayout.Controls.Add(_effectiveFormatLabel, 0, 1);
        helperLayout.Controls.Add(_formatWarningLabel, 0, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        buttons.Controls.Add(_probeFormatsButton);
        buttons.Controls.Add(_toggleBlacklistButton);
        helperLayout.Controls.Add(buttons, 0, 3);

        _formatList.Dock = DockStyle.Fill;
        helperLayout.Controls.Add(_formatList, 0, 4);

        helperGroup.Controls.Add(helperLayout);
        layout.Controls.Add(helperGroup, 1, 9);

        page.Controls.Add(layout);
        return page;
    }

    private int GetSelectedSampleRate()
    {
        return _sampleRateCombo.SelectedItem switch
        {
            int sr => sr,
            string s when int.TryParse(s, out var sr) => sr,
            _ => _config.SampleRate
        };
    }

    private void SelectSampleRate(int sampleRate)
    {
        foreach (var item in _sampleRateCombo.Items)
        {
            if (item is int sr && sr == sampleRate)
            {
                _sampleRateCombo.SelectedItem = item;
                return;
            }
        }

        _sampleRateCombo.Items.Add(sampleRate);
        _sampleRateCombo.SelectedItem = sampleRate;
    }

    private sealed record FormatProbeItem(int SampleRate, bool Supported, bool Blacklisted)
    {
        public override string ToString()
        {
            var ok = Supported ? "OK" : "NO";
            var bl = Blacklisted ? " [BLACKLIST]" : "";
            return $"{SampleRate} Hz - {ok}{bl}";
        }
    }

    private void ProbeExclusiveFormats()
    {
        try
        {
            var devName = _outputDeviceCombo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(devName))
                throw new InvalidOperationException("Select an output device first.");

            SaveConfigFromControls(showSavedDialog: false);

            using var outputDevice = DeviceHelper.GetRenderDeviceByFriendlyName(devName);
            var blacklist = new HashSet<int>(_config.BlacklistedSampleRates ?? []);

            var rates = OutputFormatNegotiator.CandidateSampleRates
                .Append(GetSelectedSampleRate())
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            _formatList.Items.Clear();
            foreach (var sr in rates)
            {
                var supported = OutputFormatNegotiator.IsExclusiveSupported(outputDevice, sr);
                _formatList.Items.Add(new FormatProbeItem(sr, supported, blacklist.Contains(sr)));
            }

            UpdateFormatInfo();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Probe failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ToggleBlacklistForSelectedRate()
    {
        if (_formatList.SelectedItem is not FormatProbeItem item)
        {
            MessageBox.Show(this, "Select a probed sample rate first.", "Blacklist", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var list = (_config.BlacklistedSampleRates ?? []).ToList();
        if (list.Contains(item.SampleRate))
            list.RemoveAll(x => x == item.SampleRate);
        else
            list.Add(item.SampleRate);

        _config.BlacklistedSampleRates = list.Distinct().OrderBy(x => x).ToArray();
        SaveConfigToDisk(showSavedDialog: false);
        ProbeExclusiveFormats();
    }

    private void UpdateFormatInfo()
    {
        try
        {
            var devName = _outputDeviceCombo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(devName))
            {
                _mixFormatLabel.Text = "";
                _effectiveFormatLabel.Text = "";
                _formatWarningLabel.Text = "";
                return;
            }

            using var outputDevice = DeviceHelper.GetRenderDeviceByFriendlyName(devName);
            var mix = outputDevice.AudioClient.MixFormat;
            _mixFormatLabel.Text = $"Windows mix: {mix.SampleRate} Hz, {mix.Channels}ch ({mix.Encoding})";

            _sampleRateCombo.Enabled = _useExclusiveMode.Checked;

            var temp = _config.Clone();
            temp.OutputRenderDevice = devName;
            temp.UseExclusiveMode = _useExclusiveMode.Checked;
            temp.SampleRate = GetSelectedSampleRate();

            var negotiation = OutputFormatNegotiator.Negotiate(temp, outputDevice);
            _effectiveFormatLabel.Text = $"Effective output: 7.1 float @ {negotiation.EffectiveConfig.SampleRate} Hz ({(temp.UseExclusiveMode ? "Exclusive" : "Shared")})";
            _formatWarningLabel.Text = negotiation.Warning ?? "";
        }
        catch (Exception ex)
        {
            _effectiveFormatLabel.Text = "";
            _formatWarningLabel.Text = ex.Message;
        }
    }

    private TabPage BuildChannelsTab()
    {
        var page = new TabPage("Channels");

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        buttons.Controls.Add(_identityMapButton);
        buttons.Controls.Add(_swapSideRearButton);
        root.Controls.Add(buttons, 0, 0);

        _identityMapButton.Click += (_, _) => SetIdentityMap();
        _swapSideRearButton.Click += (_, _) => SwapSideRear();

        var visual = BuildVisualMapGroup();
        root.Controls.Add(visual, 0, 1);

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 9,
            AutoScroll = true
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); // output label
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); // source select
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // gain slider
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));  // mute
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));  // solo
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // invert

        panel.Controls.Add(new Label { Text = "Output", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        panel.Controls.Add(new Label { Text = "Source", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 1, 0);
        panel.Controls.Add(new Label { Text = "Gain", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 2, 0);
        panel.Controls.Add(new Label { Text = "Mute", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 3, 0);
        panel.Controls.Add(new Label { Text = "Solo", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 4, 0);
        panel.Controls.Add(new Label { Text = "Invert", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 5, 0);

        for (var i = 0; i < 8; i++)
        {
            _channelLabels[i] = new Label { Text = $"0.0 dB", AutoSize = true };
            _channelSliders[i] = new TrackBar
            {
                Minimum = -240,
                Maximum = 120,
                TickFrequency = 30,
                SmallChange = 5,
                LargeChange = 10,
                Value = 0,
                Dock = DockStyle.Fill
            };

            _channelMute[i] = new CheckBox { Text = "Mute", AutoSize = true };
            _channelSolo[i] = new CheckBox { Text = "Solo", AutoSize = true };
            _channelInvert[i] = new CheckBox { Text = "Invert", AutoSize = true };

            _channelMap[i] = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            foreach (var n in ChannelNames) _channelMap[i].Items.Add(n);

            var mapIdx = i;
            _channelMap[i].SelectedIndexChanged += (_, _) => UpdateVisualMapButtons();

            var idx = i;
            _channelSliders[i].Scroll += (_, _) =>
            {
                _channelLabels[idx].Text = $"{(_channelSliders[idx].Value / 10.0):0.0} dB";
            };

            panel.Controls.Add(new Label { Text = ChannelNames[i], AutoSize = true }, 0, i + 1);
            panel.Controls.Add(_channelMap[i], 1, i + 1);

            var gainPanel = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill };
            gainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            gainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            gainPanel.Controls.Add(_channelSliders[i], 0, 0);
            gainPanel.Controls.Add(_channelLabels[i], 1, 0);
            panel.Controls.Add(gainPanel, 2, i + 1);

            panel.Controls.Add(_channelMute[i], 3, i + 1);
            panel.Controls.Add(_channelSolo[i], 4, i + 1);
            panel.Controls.Add(_channelInvert[i], 5, i + 1);
        }

        root.Controls.Add(panel, 0, 2);
        page.Controls.Add(root);
        return page;
    }

    private Control BuildVisualMapGroup()
    {
        var group = new GroupBox
        {
            Text = "Visual 7.1 map (drag tiles to swap source channels)",
            Dock = DockStyle.Top,
            AutoSize = true
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            AutoSize = true
        };
        for (var i = 0; i < 4; i++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        for (var i = 0; i < 3; i++)
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        // Positions by output-channel index: 0 FL,1 FR,2 FC,3 LFE,4 BL,5 BR,6 SL,7 SR
        AddVisualButton(grid, 0, 0, 0);
        AddVisualButton(grid, 2, 0, 1);
        AddVisualButton(grid, 1, 1, 2);
        AddVisualButton(grid, 3, 1, 3);
        AddVisualButton(grid, 0, 1, 6);
        AddVisualButton(grid, 2, 1, 7);
        AddVisualButton(grid, 0, 2, 4);
        AddVisualButton(grid, 2, 2, 5);

        group.Controls.Add(grid);
        return group;
    }

    private void AddVisualButton(TableLayoutPanel grid, int col, int row, int outputIndex)
    {
        var b = new Button
        {
            Dock = DockStyle.Fill,
            AllowDrop = true,
            Tag = outputIndex
        };

        b.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            b.DoDragDrop(outputIndex, DragDropEffects.Move);
        };

        b.DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(typeof(int)) == true)
                e.Effect = DragDropEffects.Move;
        };

        b.DragDrop += (_, e) =>
        {
            if (e.Data?.GetDataPresent(typeof(int)) != true) return;
            var from = (int)e.Data.GetData(typeof(int))!;
            var to = (int)b.Tag;
            if (from == to) return;

            var tmp = _channelMap[from].SelectedIndex;
            _channelMap[from].SelectedIndex = _channelMap[to].SelectedIndex;
            _channelMap[to].SelectedIndex = tmp;

            UpdateVisualMapButtons();
        };

        _visualMapButtons[outputIndex] = b;
        grid.Controls.Add(b, col, row);
    }

    private static string ShortName(int index) => index switch
    {
        0 => "FL",
        1 => "FR",
        2 => "FC",
        3 => "LFE",
        4 => "BL",
        5 => "BR",
        6 => "SL",
        7 => "SR",
        _ => index.ToString()
    };

    private void UpdateVisualMapButtons()
    {
        for (var outCh = 0; outCh < 8; outCh++)
        {
            var b = _visualMapButtons[outCh];
            if (b is null) continue;

            var src = _channelMap[outCh].SelectedIndex;
            if (src < 0) src = outCh;
            b.Text = $"{ShortName(outCh)} <- {ShortName(src)}";
        }
    }

    private TabPage BuildCalibrationTab()
    {
        var page = new TabPage("Calibration");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10,
            Padding = new Padding(12),
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var n in ChannelNames) _testChannelCombo.Items.Add(n);
        _testChannelCombo.SelectedIndex = 0;

        _testTypeCombo.Items.AddRange(new object[] { ToneType.Sine, ToneType.Sweep, ToneType.PinkNoise, ToneType.WhiteNoise });
        _testTypeCombo.SelectedItem = ToneType.Sine;

        _testPresetCombo.Items.AddRange(new object[]
        {
            "Manual",
            "Identify (Sine)",
            "Level match (Pink)",
            "Alternate (Sine/Pink per step)"
        });
        _testPresetCombo.SelectedIndex = 0;

        _testFreq.Value = 440;
        _testFreqEnd.Value = 2000;
        _testSweepSec.Value = 5;
        _testLevelDb.Value = -18;
        _testStepMs.Value = 2000;
        _testLoop.Checked = true;

        layout.Controls.Add(new Label { Text = "Channel", AutoSize = true }, 0, 0);
        layout.Controls.Add(_testChannelCombo, 1, 0);

        layout.Controls.Add(new Label { Text = "Preset", AutoSize = true }, 0, 1);
        layout.Controls.Add(_testPresetCombo, 1, 1);

        layout.Controls.Add(new Label { Text = "Signal", AutoSize = true }, 0, 2);
        layout.Controls.Add(_testTypeCombo, 1, 2);

        layout.Controls.Add(new Label { Text = "Frequency (Hz)", AutoSize = true }, 0, 3);
        layout.Controls.Add(_testFreq, 1, 3);

        layout.Controls.Add(new Label { Text = "Sweep end (Hz)", AutoSize = true }, 0, 4);
        layout.Controls.Add(_testFreqEnd, 1, 4);

        layout.Controls.Add(new Label { Text = "Sweep time (sec)", AutoSize = true }, 0, 5);
        layout.Controls.Add(_testSweepSec, 1, 5);

        layout.Controls.Add(new Label { Text = "Level (dB)", AutoSize = true }, 0, 6);
        layout.Controls.Add(_testLevelDb, 1, 6);

        var optionsRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        optionsRow.Controls.Add(_testVoicePrompts);
        optionsRow.Controls.Add(_testAutoStep);
        optionsRow.Controls.Add(_testLoop);
        layout.Controls.Add(optionsRow, 1, 7);

        var stepRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        stepRow.Controls.Add(new Label { Text = "Step (ms)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        stepRow.Controls.Add(_testStepMs);
        layout.Controls.Add(stepRow, 1, 8);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        buttons.Controls.Add(_testStartButton);
        buttons.Controls.Add(_testStopButton);
        layout.Controls.Add(buttons, 1, 9);

        _testStartButton.Click += (_, _) => StartTest();
        _testStopButton.Click += (_, _) => StopTest();

        _testFreq.ValueChanged += (_, _) => { if (_tonePlayer is not null) _tonePlayer.SetFrequency((float)_testFreq.Value); };
        _testFreqEnd.ValueChanged += (_, _) => { if (_tonePlayer is not null) _tonePlayer.SetSweepEndFrequency((float)_testFreqEnd.Value); };
        _testSweepSec.ValueChanged += (_, _) => { if (_tonePlayer is not null) _tonePlayer.SetSweepSeconds((float)_testSweepSec.Value); };
        _testLevelDb.ValueChanged += (_, _) => { if (_tonePlayer is not null) _tonePlayer.SetLevelDb((float)_testLevelDb.Value); };

        _testPresetCombo.SelectedIndexChanged += (_, _) => ApplyCalibrationPresetToControls();

        _testChannelCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_tonePlayer is null) return;
            _tonePlayer.SetChannel(_testChannelCombo.SelectedIndex);
            if (_testVoicePrompts.Checked)
                VoicePrompter.Speak(ChannelNames[_testChannelCombo.SelectedIndex]);
        };

        page.Controls.Add(layout);
        return page;
    }

    private void RefreshDeviceLists()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => d.FriendlyName)
            .ToList();

        var captureDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(d => d.FriendlyName)
            .ToList();

        void SetItems(ComboBox combo)
        {
            var selected = combo.SelectedItem as string;
            combo.Items.Clear();
            foreach (var name in devices) combo.Items.Add(name);
            if (!string.IsNullOrWhiteSpace(selected) && combo.Items.Contains(selected))
                combo.SelectedItem = selected;
        }

        SetItems(_musicDeviceCombo);
        SetItems(_shakerDeviceCombo);
        SetItems(_outputDeviceCombo);

        // capture list
        {
            var selected = _latencyInputCombo.SelectedItem as string;
            _latencyInputCombo.Items.Clear();
            foreach (var name in captureDevices) _latencyInputCombo.Items.Add(name);
            if (!string.IsNullOrWhiteSpace(selected) && _latencyInputCombo.Items.Contains(selected))
                _latencyInputCombo.SelectedItem = selected;
        }
    }

    private void LoadConfigIntoControls()
    {
        SelectIfPresent(_musicDeviceCombo, _config.MusicInputRenderDevice);
        SelectIfPresent(_shakerDeviceCombo, _config.ShakerInputRenderDevice);
        SelectIfPresent(_outputDeviceCombo, _config.OutputRenderDevice);
        if (!string.IsNullOrWhiteSpace(_config.LatencyInputCaptureDevice))
            SelectIfPresent(_latencyInputCombo, _config.LatencyInputCaptureDevice);

        _musicGainDb.Value = (decimal)_config.MusicGainDb;
        _shakerGainDb.Value = (decimal)_config.ShakerGainDb;

        _hpHz.Value = (decimal)_config.ShakerHighPassHz;
        _lpHz.Value = (decimal)_config.ShakerLowPassHz;

        _mixingModeCombo.SelectedIndex = ((_config.MixingMode ?? "FrontBoth").Trim()) switch
        {
            "Dedicated" => 1,
            _ => 0
        };

        _latencyMs.Value = _config.LatencyMs;
        _useCenter.Checked = _config.UseCenterChannel;
        _useExclusiveMode.Checked = _config.UseExclusiveMode;
        SelectSampleRate(_config.SampleRate);

        var gains = _config.ChannelGainsDb ?? new float[8];
        var map = _config.OutputChannelMap ?? [0, 1, 2, 3, 4, 5, 6, 7];
        var mute = _config.ChannelMute ?? [false, false, false, false, false, false, false, false];
        var invert = _config.ChannelInvert ?? [false, false, false, false, false, false, false, false];
        var solo = _config.ChannelSolo ?? [false, false, false, false, false, false, false, false];
        for (var i = 0; i < 8; i++)
        {
            var db = gains[i];
            var tenths = (int)Math.Round(db * 10.0);
            tenths = Math.Clamp(tenths, _channelSliders[i].Minimum, _channelSliders[i].Maximum);
            _channelSliders[i].Value = tenths;
            _channelLabels[i].Text = $"{(tenths / 10.0):0.0} dB";

            var src = Math.Clamp(map[i], 0, 7);
            _channelMap[i].SelectedIndex = src;
            _channelMute[i].Checked = mute[i];
            _channelSolo[i].Checked = solo[i];
            _channelInvert[i].Checked = invert[i];
        }

        _testVoicePrompts.Checked = _config.EnableVoicePrompts;
        _testAutoStep.Checked = _config.CalibrationAutoStep;
        _testStepMs.Value = _config.CalibrationStepMs;
        _testLoop.Checked = _config.CalibrationLoop;

        _testPresetCombo.SelectedIndex = _config.CalibrationPreset switch
        {
            "IdentifySine" => 1,
            "LevelPink" => 2,
            "AlternateSinePink" => 3,
            _ => 0
        };

        ApplyCalibrationPresetToControls();

        UpdateVisualMapButtons();
        UpdateFormatInfo();
    }

    private void SaveConfigFromControls(bool showSavedDialog = true)
    {
        _config.MusicInputRenderDevice = _musicDeviceCombo.SelectedItem as string ?? _config.MusicInputRenderDevice;
        _config.ShakerInputRenderDevice = _shakerDeviceCombo.SelectedItem as string ?? _config.ShakerInputRenderDevice;
        _config.OutputRenderDevice = _outputDeviceCombo.SelectedItem as string ?? _config.OutputRenderDevice;
        _config.LatencyInputCaptureDevice = _latencyInputCombo.SelectedItem as string ?? _config.LatencyInputCaptureDevice;

        _config.MusicGainDb = (float)_musicGainDb.Value;
        _config.ShakerGainDb = (float)_shakerGainDb.Value;

        _config.ShakerHighPassHz = (float)_hpHz.Value;
        _config.ShakerLowPassHz = (float)_lpHz.Value;

        _config.MixingMode = _mixingModeCombo.SelectedIndex switch
        {
            1 => "Dedicated",
            _ => "FrontBoth"
        };

        _config.LatencyMs = (int)_latencyMs.Value;
        _config.UseCenterChannel = _useCenter.Checked;
        _config.UseExclusiveMode = _useExclusiveMode.Checked;
        _config.SampleRate = GetSelectedSampleRate();

        _config.EnableVoicePrompts = _testVoicePrompts.Checked;
        _config.CalibrationAutoStep = _testAutoStep.Checked;
        _config.CalibrationStepMs = (int)_testStepMs.Value;
        _config.CalibrationLoop = _testLoop.Checked;

        _config.CalibrationPreset = _testPresetCombo.SelectedIndex switch
        {
            1 => "IdentifySine",
            2 => "LevelPink",
            3 => "AlternateSinePink",
            _ => "Manual"
        };

        var channel = new float[8];
        for (var i = 0; i < 8; i++)
        {
            channel[i] = _channelSliders[i].Value / 10.0f;
        }
        _config.ChannelGainsDb = channel;

        var map = new int[8];
        var mute = new bool[8];
        var solo = new bool[8];
        var invert = new bool[8];
        for (var i = 0; i < 8; i++)
        {
            map[i] = _channelMap[i].SelectedIndex < 0 ? i : _channelMap[i].SelectedIndex;
            mute[i] = _channelMute[i].Checked;
            solo[i] = _channelSolo[i].Checked;
            invert[i] = _channelInvert[i].Checked;
        }
        _config.OutputChannelMap = map;
        _config.ChannelMute = mute;
        _config.ChannelSolo = solo;
        _config.ChannelInvert = invert;

        _config.Validate();
        SaveConfigToDisk(showSavedDialog);

        UpdateFormatInfo();
    }

    private void SaveConfigToDisk(bool showSavedDialog)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, options));

        if (showSavedDialog)
            MessageBox.Show(this, "Saved.", "CM6206 Dual Router", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void StartTest()
    {
        if (_tonePlayer is not null)
            return;

        try
        {
            // Don’t run router and test simultaneously.
            StopRouter();

            SaveConfigFromControls(showSavedDialog: false);
            _config = RouterConfig.Load(_configPath);

            _tonePlayer = new TonePlayer(_config);
            _tonePlayer.SetChannel(_testChannelCombo.SelectedIndex);
            _tonePlayer.SetType((ToneType)_testTypeCombo.SelectedItem!);
            _tonePlayer.SetFrequency((float)_testFreq.Value);
            _tonePlayer.SetSweepEndFrequency((float)_testFreqEnd.Value);
            _tonePlayer.SetSweepSeconds((float)_testSweepSec.Value);
            _tonePlayer.SetSweepLoop(true);
            _tonePlayer.SetLevelDb((float)_testLevelDb.Value);
            _tonePlayer.Start();

            if (_testVoicePrompts.Checked)
                VoicePrompter.Speak(ChannelNames[_testChannelCombo.SelectedIndex]);

            if (_testAutoStep.Checked)
            {
                _autoStepTimer.Interval = (int)_testStepMs.Value;
                _autoStepTimer.Start();
            }

            _testStartButton.Enabled = false;
            _testStopButton.Enabled = true;
        }
        catch (Exception ex)
        {
            StopTest();
            MessageBox.Show(this, ex.Message, "Failed to start test", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopTest()
    {
        if (_tonePlayer is null)
            return;

        try
        {
            _autoStepTimer.Stop();
            _tonePlayer.Stop();
            _tonePlayer.Dispose();
        }
        finally
        {
            _tonePlayer = null;
            _testStartButton.Enabled = true;
            _testStopButton.Enabled = false;
        }
    }

    private void AutoStepTick()
    {
        if (_tonePlayer is null)
        {
            _autoStepTimer.Stop();
            return;
        }

        var next = _testChannelCombo.SelectedIndex + 1;
        if (next >= 8)
        {
            if (_testLoop.Checked)
            {
                next = 0;
            }
            else
            {
                _autoStepTimer.Stop();
                return;
            }
        }

        // This triggers SelectedIndexChanged handler which updates tone channel + speaks (if enabled).
        _testChannelCombo.SelectedIndex = next;

        if (_tonePlayer is null)
            return;

        // Optional: alternate between sine and pink noise each step.
        if (_testPresetCombo.SelectedIndex == 3)
        {
            _alternateIsSine = !_alternateIsSine;
            var nextType = _alternateIsSine ? ToneType.Sine : ToneType.PinkNoise;
            _testTypeCombo.SelectedItem = nextType;
            _tonePlayer.SetType(nextType);

            if (nextType == ToneType.Sine)
            {
                _tonePlayer.SetFrequency((float)_testFreq.Value);
            }
        }
    }

    private void ApplyCalibrationPresetToControls()
    {
        // 0 Manual
        // 1 Identify (Sine)
        // 2 Level match (Pink)
        // 3 Alternate
        switch (_testPresetCombo.SelectedIndex)
        {
            case 1:
                _testTypeCombo.SelectedItem = ToneType.Sine;
                _testTypeCombo.Enabled = false;
                break;
            case 2:
                _testTypeCombo.SelectedItem = ToneType.PinkNoise;
                _testTypeCombo.Enabled = false;
                break;
            case 3:
                _alternateIsSine = true;
                _testTypeCombo.SelectedItem = ToneType.Sine;
                _testTypeCombo.Enabled = false;
                break;
            default:
                _testTypeCombo.Enabled = true;
                break;
        }

        UpdateCalibrationControlEnables();

        _testTypeCombo.SelectedIndexChanged -= TestTypeComboOnSelectedIndexChanged;
        _testTypeCombo.SelectedIndexChanged += TestTypeComboOnSelectedIndexChanged;
    }

    private void TestTypeComboOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        var t = (ToneType)_testTypeCombo.SelectedItem!;
        UpdateCalibrationControlEnables();
        if (_tonePlayer is null) return;

        _tonePlayer.SetType(t);
        if (t == ToneType.Sweep)
        {
            _tonePlayer.SetFrequency((float)_testFreq.Value);
            _tonePlayer.SetSweepEndFrequency((float)_testFreqEnd.Value);
            _tonePlayer.SetSweepSeconds((float)_testSweepSec.Value);
        }
        else if (t == ToneType.Sine)
        {
            _tonePlayer.SetFrequency((float)_testFreq.Value);
        }
    }

    private void UpdateCalibrationControlEnables()
    {
        var t = (ToneType)_testTypeCombo.SelectedItem!;
        var isSine = t == ToneType.Sine;
        var isSweep = t == ToneType.Sweep;

        _testFreq.Enabled = isSine || isSweep;
        _testFreqEnd.Enabled = isSweep;
        _testSweepSec.Enabled = isSweep;
    }

    private void StartRouter()
    {
        if (_router is not null)
            return;

        try
        {
            SaveConfigFromControls(showSavedDialog: false);
            _config = RouterConfig.Load(_configPath);
            _router = new WasapiDualRouter(_config);
            _router.Start();

            _metersTimer.Enabled = true;

            if (!string.IsNullOrWhiteSpace(_router.FormatWarning))
            {
                _formatWarningLabel.Text = _router.FormatWarning;
                _effectiveFormatLabel.Text = $"Effective output: 7.1 float @ {_router.EffectiveSampleRate} Hz ({(_config.UseExclusiveMode ? "Exclusive" : "Shared")})";
            }

            _startButton.Enabled = false;
            _stopButton.Enabled = true;

            UpdateDiagnostics();
        }
        catch (Exception ex)
        {
            StopRouter();
            MessageBox.Show(this, ex.Message, "Failed to start", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopRouter()
    {
        if (_router is null)
            return;

        try
        {
            _router.Stop();
            _router.Dispose();
        }
        finally
        {
            _router = null;

            _metersTimer.Enabled = false;
            ClearMeters();

            _startButton.Enabled = true;
            _stopButton.Enabled = false;

            UpdateDiagnostics();
        }
    }

    private static void SelectIfPresent(ComboBox combo, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (combo.Items.Contains(value))
        {
            combo.SelectedItem = value;
            return;
        }

        // best-effort fallback: contains match
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is string s && s.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    private void SetIdentityMap()
    {
        for (var i = 0; i < 8; i++)
            _channelMap[i].SelectedIndex = i;
    }

    private void SwapSideRear()
    {
        // Output channels are: 4 BL,5 BR,6 SL,7 SR.
        // Swap source assignment between rear and side.
        _channelMap[4].SelectedIndex = 6;
        _channelMap[5].SelectedIndex = 7;
        _channelMap[6].SelectedIndex = 4;
        _channelMap[7].SelectedIndex = 5;
    }
}
