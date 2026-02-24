using System.Text.Json;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace Cm6206DualRouter;

public sealed class RouterMainForm : Form
{
    private readonly string _configPath;

    private readonly ComboBox _musicDeviceCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _shakerDeviceCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _outputDeviceCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly NumericUpDown _musicGainDb = new() { Minimum = -60, Maximum = 20, DecimalPlaces = 1, Increment = 0.5M };
    private readonly NumericUpDown _shakerGainDb = new() { Minimum = -60, Maximum = 20, DecimalPlaces = 1, Increment = 0.5M };

    private readonly NumericUpDown _hpHz = new() { Minimum = 1, Maximum = 300, DecimalPlaces = 1, Increment = 1 };
    private readonly NumericUpDown _lpHz = new() { Minimum = 5, Maximum = 300, DecimalPlaces = 1, Increment = 1 };

    private readonly NumericUpDown _latencyMs = new() { Minimum = 10, Maximum = 500, DecimalPlaces = 0, Increment = 5 };

    private readonly CheckBox _useCenter = new() { Text = "Use Center channel" };

    private readonly TrackBar[] _channelSliders = new TrackBar[8];
    private readonly Label[] _channelLabels = new Label[8];
    private readonly CheckBox[] _channelMute = new CheckBox[8];
    private readonly CheckBox[] _channelInvert = new CheckBox[8];
    private readonly ComboBox[] _channelMap = new ComboBox[8];

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
    private readonly NumericUpDown _testLevelDb = new() { Minimum = -60, Maximum = 0, DecimalPlaces = 1, Increment = 1 };
    private readonly Button _testStartButton = new() { Text = "Start test" };
    private readonly Button _testStopButton = new() { Text = "Stop test", Enabled = false };

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

        _config = RouterConfig.Load(_configPath);

        var tabs = new TabControl { Dock = DockStyle.Fill };

        tabs.TabPages.Add(BuildDevicesTab());
        tabs.TabPages.Add(BuildDspTab());
        tabs.TabPages.Add(BuildChannelsTab());
        tabs.TabPages.Add(BuildCalibrationTab());

        Controls.Add(tabs);

        FormClosing += (_, _) => StopRouter();

        RefreshDeviceLists();
        LoadConfigIntoControls();
    }

    private TabPage BuildDevicesTab()
    {
        var page = new TabPage("Devices");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
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

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        buttons.Controls.Add(_refreshButton);
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(_startButton);
        buttons.Controls.Add(_stopButton);

        layout.Controls.Add(buttons, 1, 3);

        _refreshButton.Click += (_, _) => RefreshDeviceLists();
        _saveButton.Click += (_, _) => SaveConfigFromControls();
        _startButton.Click += (_, _) => StartRouter();
        _stopButton.Click += (_, _) => StopRouter();

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildDspTab()
    {
        var page = new TabPage("DSP");

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

        layout.Controls.Add(new Label { Text = "Music gain (dB)", AutoSize = true }, 0, 0);
        layout.Controls.Add(_musicGainDb, 1, 0);

        layout.Controls.Add(new Label { Text = "Shaker gain (dB)", AutoSize = true }, 0, 1);
        layout.Controls.Add(_shakerGainDb, 1, 1);

        layout.Controls.Add(new Label { Text = "Shaker high-pass (Hz)", AutoSize = true }, 0, 2);
        layout.Controls.Add(_hpHz, 1, 2);

        layout.Controls.Add(new Label { Text = "Shaker low-pass (Hz)", AutoSize = true }, 0, 3);
        layout.Controls.Add(_lpHz, 1, 3);

        layout.Controls.Add(new Label { Text = "Latency (ms)", AutoSize = true }, 0, 4);
        layout.Controls.Add(_latencyMs, 1, 4);

        layout.Controls.Add(_useCenter, 1, 5);

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildChannelsTab()
    {
        var page = new TabPage("Channels");

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12)
        };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        buttons.Controls.Add(_identityMapButton);
        buttons.Controls.Add(_swapSideRearButton);
        root.Controls.Add(buttons, 0, 0);

        _identityMapButton.Click += (_, _) => SetIdentityMap();
        _swapSideRearButton.Click += (_, _) => SwapSideRear();

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 9,
            AutoScroll = true
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); // output label
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); // source select
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // gain slider
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));  // mute
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // invert

        panel.Controls.Add(new Label { Text = "Output", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        panel.Controls.Add(new Label { Text = "Source", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 1, 0);
        panel.Controls.Add(new Label { Text = "Gain", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 2, 0);
        panel.Controls.Add(new Label { Text = "Mute", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 3, 0);
        panel.Controls.Add(new Label { Text = "Invert", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 4, 0);

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
            _channelInvert[i] = new CheckBox { Text = "Invert", AutoSize = true };

            _channelMap[i] = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            foreach (var n in ChannelNames) _channelMap[i].Items.Add(n);

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
            panel.Controls.Add(_channelInvert[i], 4, i + 1);
        }

        root.Controls.Add(panel, 0, 1);
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildCalibrationTab()
    {
        var page = new TabPage("Calibration");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(12),
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var n in ChannelNames) _testChannelCombo.Items.Add(n);
        _testChannelCombo.SelectedIndex = 0;

        _testTypeCombo.Items.AddRange(new object[] { ToneType.Sine, ToneType.PinkNoise, ToneType.WhiteNoise });
        _testTypeCombo.SelectedItem = ToneType.Sine;

        _testFreq.Value = 440;
        _testLevelDb.Value = -18;

        layout.Controls.Add(new Label { Text = "Channel", AutoSize = true }, 0, 0);
        layout.Controls.Add(_testChannelCombo, 1, 0);

        layout.Controls.Add(new Label { Text = "Signal", AutoSize = true }, 0, 1);
        layout.Controls.Add(_testTypeCombo, 1, 1);

        layout.Controls.Add(new Label { Text = "Frequency (Hz)", AutoSize = true }, 0, 2);
        layout.Controls.Add(_testFreq, 1, 2);

        layout.Controls.Add(new Label { Text = "Level (dB)", AutoSize = true }, 0, 3);
        layout.Controls.Add(_testLevelDb, 1, 3);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        buttons.Controls.Add(_testStartButton);
        buttons.Controls.Add(_testStopButton);
        layout.Controls.Add(buttons, 1, 4);

        _testStartButton.Click += (_, _) => StartTest();
        _testStopButton.Click += (_, _) => StopTest();

        page.Controls.Add(layout);
        return page;
    }

    private void RefreshDeviceLists()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
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
    }

    private void LoadConfigIntoControls()
    {
        SelectIfPresent(_musicDeviceCombo, _config.MusicInputRenderDevice);
        SelectIfPresent(_shakerDeviceCombo, _config.ShakerInputRenderDevice);
        SelectIfPresent(_outputDeviceCombo, _config.OutputRenderDevice);

        _musicGainDb.Value = (decimal)_config.MusicGainDb;
        _shakerGainDb.Value = (decimal)_config.ShakerGainDb;

        _hpHz.Value = (decimal)_config.ShakerHighPassHz;
        _lpHz.Value = (decimal)_config.ShakerLowPassHz;

        _latencyMs.Value = _config.LatencyMs;
        _useCenter.Checked = _config.UseCenterChannel;

        var gains = _config.ChannelGainsDb ?? new float[8];
        var map = _config.OutputChannelMap ?? [0, 1, 2, 3, 4, 5, 6, 7];
        var mute = _config.ChannelMute ?? [false, false, false, false, false, false, false, false];
        var invert = _config.ChannelInvert ?? [false, false, false, false, false, false, false, false];
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
            _channelInvert[i].Checked = invert[i];
        }
    }

    private void SaveConfigFromControls()
    {
        _config.MusicInputRenderDevice = _musicDeviceCombo.SelectedItem as string ?? _config.MusicInputRenderDevice;
        _config.ShakerInputRenderDevice = _shakerDeviceCombo.SelectedItem as string ?? _config.ShakerInputRenderDevice;
        _config.OutputRenderDevice = _outputDeviceCombo.SelectedItem as string ?? _config.OutputRenderDevice;

        _config.MusicGainDb = (float)_musicGainDb.Value;
        _config.ShakerGainDb = (float)_shakerGainDb.Value;

        _config.ShakerHighPassHz = (float)_hpHz.Value;
        _config.ShakerLowPassHz = (float)_lpHz.Value;

        _config.LatencyMs = (int)_latencyMs.Value;
        _config.UseCenterChannel = _useCenter.Checked;

        var channel = new float[8];
        for (var i = 0; i < 8; i++)
        {
            channel[i] = _channelSliders[i].Value / 10.0f;
        }
        _config.ChannelGainsDb = channel;

        var map = new int[8];
        var mute = new bool[8];
        var invert = new bool[8];
        for (var i = 0; i < 8; i++)
        {
            map[i] = _channelMap[i].SelectedIndex < 0 ? i : _channelMap[i].SelectedIndex;
            mute[i] = _channelMute[i].Checked;
            invert[i] = _channelInvert[i].Checked;
        }
        _config.OutputChannelMap = map;
        _config.ChannelMute = mute;
        _config.ChannelInvert = invert;

        _config.Validate();

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, options));

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

            SaveConfigFromControls();
            _config = RouterConfig.Load(_configPath);

            _tonePlayer = new TonePlayer(_config);
            _tonePlayer.SetChannel(_testChannelCombo.SelectedIndex);
            _tonePlayer.SetType((ToneType)_testTypeCombo.SelectedItem!);
            _tonePlayer.SetFrequency((float)_testFreq.Value);
            _tonePlayer.SetLevelDb((float)_testLevelDb.Value);
            _tonePlayer.Start();

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

    private void StartRouter()
    {
        if (_router is not null)
            return;

        try
        {
            SaveConfigFromControls();
            _config = RouterConfig.Load(_configPath);
            _router = new WasapiDualRouter(_config);
            _router.Start();

            _startButton.Enabled = false;
            _stopButton.Enabled = true;
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
            _startButton.Enabled = true;
            _stopButton.Enabled = false;
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
