using System.Text.Json;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.IO;

namespace Cm6206DualRouter;

public sealed class RouterMainForm : Form
{
    private readonly string _configPath;

    private readonly SplitContainer _mainSplit = new()
    {
        Dock = DockStyle.Fill,
        FixedPanel = FixedPanel.Panel2,
        SplitterWidth = 6
    };

    private readonly SetupAssistantPanel _assistant = new();

    private AiSettings _aiSettings;

    private readonly AiCopilotService _aiCopilot;

    private UiState _uiState;
    private string? _lastStartError;
    private DateTime _lastOutputCheckUtc = DateTime.MinValue;
    private bool _cachedOutputOk = true;

    private bool _deviceRefreshRunning;
    private bool _suppressFormatUpdate;
    private int _formatInfoRequestId;

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private TabPage? _simplePage;
    private TabPage? _devicesPage;
    private TabPage? _diagnosticsPage;
    private TabPage? _dspPage;
    private TabPage? _routingPage;
    private TabPage? _channelsPage;
    private TabPage? _metersPage;
    private TabPage? _calibrationPage;

    // Simple Mode controls
    private readonly ComboBox _simpleGameSourceCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 520 };
    private readonly ComboBox _simpleSecondarySourceCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 520 };
    private readonly ComboBox _simpleOutputCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 520 };

    private readonly ComboBox _simplePresetCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly Label _simplePresetSummary = new() { AutoSize = true, ForeColor = NeonTheme.TextMuted };

    private readonly NeonSlider _simpleMasterGain = new() { Minimum = -600, Maximum = 200, Value = 0, Width = 260 };
    private readonly Label _simpleMasterGainLabel = new() { Text = "0.0 dB", AutoSize = true, ForeColor = NeonTheme.TextSecondary };

    private readonly NeonSlider _simpleShakerStrength = new() { Minimum = -240, Maximum = 120, Value = -60, Width = 260 };
    private readonly Label _simpleShakerStrengthLabel = new() { Text = "-6.0 dB", AutoSize = true, ForeColor = NeonTheme.TextSecondary };

    private readonly Button _simpleStartButton = new() { Text = "▶ Start Routing", Width = 260, Height = 44 };
    private readonly Button _simpleStopButton = new() { Text = "■ Stop", Width = 120, Height = 44, Enabled = false };
    private readonly CheckBox _simpleAdvancedToggle = new() { Text = "⚙ Advanced Controls", AutoSize = true };
    private readonly Label _simpleStatus = new() { Text = "", AutoSize = true };
    private readonly Label _simpleNextHint = new()
    {
        Text = "New here?  1) Select devices   2) Pick a preset   3) Press Start",
        AutoSize = true,
        ForeColor = NeonTheme.TextMuted
    };

    private readonly SignalFlowControl _simpleFlow = new() { Dock = DockStyle.Top };
    private readonly ToolTip _toolTip = new() { AutomaticDelay = 200, AutoPopDelay = 6000, ReshowDelay = 200, InitialDelay = 250, ShowAlways = true };

    private readonly ComboBox _profileCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _profileLoadButton = new() { Text = "Load" };
    private readonly Button _profileSaveAsButton = new() { Text = "Save As" };
    private readonly Button _profileDeleteButton = new() { Text = "Delete" };
    private readonly Button _profileImportButton = new() { Text = "Import..." };
    private readonly Button _profileOpenFolderButton = new() { Text = "Open folder" };
    private readonly Button _presetMovieButton = new() { Text = "Movie" };
    private readonly Button _presetMusicButton = new() { Text = "Music" };
    private readonly Button _presetGameButton = new() { Text = "Game" };
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
    private readonly NumericUpDown _masterGainDb = new() { Minimum = -60, Maximum = 20, DecimalPlaces = 1, Increment = 0.5M };

    private readonly CheckBox _musicHpEnable = new() { Text = "Enable", AutoSize = true };
    private readonly NumericUpDown _musicHpHz = new() { Minimum = 1, Maximum = 300, DecimalPlaces = 1, Increment = 1, Width = 100 };
    private readonly CheckBox _musicLpEnable = new() { Text = "Enable", AutoSize = true };
    private readonly NumericUpDown _musicLpHz = new() { Minimum = 5, Maximum = 300, DecimalPlaces = 1, Increment = 1, Width = 100 };

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

    private readonly DataGridView _routingGrid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
    };

    private readonly StatusStrip _statusStrip = new() { Dock = DockStyle.Bottom };
    private readonly ToolStripStatusLabel _statusHealth = new() { Text = "", Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
    private readonly ToolStripStatusLabel _statusRouter = new() { Text = "Router: (unknown)" };
    private readonly ToolStripStatusLabel _statusSpacer1 = new() { Spring = true };
    private readonly ToolStripStatusLabel _statusFormat = new() { Text = "Format: (unknown)" };
    private readonly ToolStripStatusLabel _statusSpacer2 = new() { Spring = true };
    private readonly ToolStripStatusLabel _statusLatency = new() { Text = "Latency: (unknown)" };

    private readonly ToolStripStatusLabel _statusUpdate = new()
    {
        Text = "↑",
        Visible = false,
        AutoSize = true,
        ForeColor = NeonTheme.MeterLow,
        ToolTipText = "Update available"
    };

    private readonly System.Windows.Forms.Timer _statusTimer = new();

    private readonly ToolStripDropDownButton _statusPreset = new() { Text = "Preset" };

    private UpdateInfo? _availableUpdate;

    // SÖNDBÖUND console (Routing tab)
    private readonly NeonMatrixControl _consoleMatrix = new(rows: 6, cols: 2);
    private readonly NeonMeter _consoleMeterA = new() { Vertical = true, Width = 18, Height = 160 };
    private readonly NeonMeter _consoleMeterB = new() { Vertical = true, Width = 18, Height = 160 };

    private readonly NeonSlider _consoleMusicGain = new() { Minimum = -600, Maximum = 200, Value = 0, Width = 200 };
    private readonly Label _consoleMusicGainLabel = new() { Text = "0.0 dB", AutoSize = true };
    private readonly NeonSlider _consoleShakerGain = new() { Minimum = -600, Maximum = 200, Value = 0, Width = 200 };
    private readonly Label _consoleShakerGainLabel = new() { Text = "0.0 dB", AutoSize = true };

    private readonly ComboBox _consoleInputMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };

    private readonly NeonMeter[] _consoleOutMeters = new NeonMeter[8];
    private readonly NeonSlider[] _consoleOutGain = new NeonSlider[8];
    private readonly Label[] _consoleOutGainLabel = new Label[8];
    private readonly NeonToggleButton[] _consoleOutMute = new NeonToggleButton[8];
    private readonly NeonToggleButton[] _consoleOutSolo = new NeonToggleButton[8];

    private bool _suppressConsoleSync;
    private bool _consoleMatrixDirty;

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

        AppLog.Info("RouterMainForm ctor: begin");

        _aiCopilot = new AiCopilotService(new OpenAiClient(new HttpClient()));
        _configPath = configPath;
        Text = "CM6206 Dual Router";
        Width = 860;
        Height = 620;
        StartPosition = FormStartPosition.CenterScreen;

        DoubleBuffered = true;
        BackColor = NeonTheme.BgPrimary;
        ForeColor = NeonTheme.TextPrimary;
        Font = NeonTheme.CreateBaseFont(13);

        _uiState = UiStateStore.Load();
        _aiSettings = AiSettingsStore.Load();

        _config = LoadOrCreateConfigForUi(_configPath);

        AppLog.Info("RouterMainForm ctor: config loaded");

        _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabs.Padding = new Point(14, 6);
        _tabs.DrawItem += (_, e) =>
        {
            var page = _tabs.TabPages[e.Index];
            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using var bg = new SolidBrush(selected ? NeonTheme.BgRaised : NeonTheme.BgPanel);
            e.Graphics.FillRectangle(bg, e.Bounds);

            var textColor = selected ? NeonTheme.TextPrimary : NeonTheme.TextSecondary;
            using var brush = new SolidBrush(textColor);
            var textRect = e.Bounds;
            textRect.Inflate(-10, -4);
            TextRenderer.DrawText(e.Graphics, page.Text, Font, textRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            if (selected)
            {
                using var pen = new Pen(Color.FromArgb(180, NeonTheme.NeonCyan), 2f);
                e.Graphics.DrawLine(pen, e.Bounds.Left + 2, e.Bounds.Bottom - 2, e.Bounds.Right - 2, e.Bounds.Bottom - 2);
            }
        };

        _simplePage = BuildSimpleTab();
        _devicesPage = BuildDevicesTab();
        _diagnosticsPage = BuildDiagnosticsTab();
        _dspPage = BuildDspTab();
        _routingPage = BuildRoutingTab();
        _channelsPage = BuildChannelsTab();
        _metersPage = BuildMetersTab();
        _calibrationPage = BuildCalibrationTab();

        AppLog.Info("RouterMainForm ctor: tabs built");

        RebuildTabs(showAdvanced: _uiState.ShowAdvancedControls);
        _mainSplit.Panel1.Controls.Add(_tabs);
        _mainSplit.Panel2.Controls.Add(_assistant);
        _mainSplit.SplitterDistance = Math.Max(520, Width - 340);
        _mainSplit.Panel2MinSize = 300;
        Controls.Add(_mainSplit);

        _statusStrip.BackColor = NeonTheme.BgPanel;
        _statusStrip.ForeColor = NeonTheme.TextSecondary;
        _statusUpdate.BorderSides = ToolStripStatusLabelBorderSides.All;
        _statusUpdate.BorderStyle = Border3DStyle.Etched;
        _statusUpdate.Margin = new Padding(4, 2, 4, 2);
        _statusUpdate.Padding = new Padding(8, 0, 8, 0);
        _statusUpdate.Click += async (_, _) => await OnUpdateClickedAsync();

        _statusStrip.Items.AddRange(new ToolStripItem[] { _statusHealth, _statusRouter, _statusSpacer1, _statusFormat, _statusSpacer2, _statusLatency, _statusUpdate, _statusPreset });
        Controls.Add(_statusStrip);

        ApplyNeonTheme(this);

        AppLog.Info("RouterMainForm ctor: theme applied");

        _assistant.LoadSettings(_aiSettings);
        _assistant.GetContext = BuildCopilotContext;
        _assistant.SaveSettings = s =>
        {
            _aiSettings = s;
            AiSettingsStore.Save(_aiSettings);
        };
        _assistant.ApplyActionsWithConfirmation = actions => ApplyCopilotActionsWithConfirmation(actions);
        _assistant.RunAiCommandAsync = RunAiCommandAsync;
        _assistant.ExplainAsync = ExplainCurrentScreenAsync;

        _autoStepTimer.Tick += (_, _) => AutoStepTick();
        _profilePollTimer.Tick += (_, _) => AutoProfileSwitchTick();
        _metersTimer.Interval = 16;
        _metersTimer.Tick += (_, _) => UpdateMetersTick();

        _statusTimer.Interval = 250;
        _statusTimer.Tick += (_, _) => UpdateStatusBar();
        _statusTimer.Enabled = true;

        FormClosing += (_, _) =>
        {
            StopTest();
            StopRouter();
            VoicePrompter.Dispose();
        };

        // Keep constructor fast: if audio device enumeration hangs on a system,
        // doing it here prevents the window from ever showing.
        LoadConfigIntoControls();
        WireFormatUi();
        RefreshProfilesCombo();
        UpdateDiagnostics();
        UpdateStatusBar();

        Shown += async (_, _) =>
        {
            AppLog.Info("RouterMainForm: Shown event");
            await StartupAfterShownAsync();
        };

        AppLog.Info("RouterMainForm ctor: end");
    }

    private async Task StartupAfterShownAsync()
    {
        try
        {
            AppLog.Info("UI shown; starting async device refresh...");
            await RefreshDeviceListsAsync(showErrorDialog: false);
            LoadDeviceSelectionsFromConfig();
            UpdateStatusBar();
            UpdateFormatInfo();

            _ = CheckForUpdatesAfterStartupAsync();
        }
        catch (Exception ex)
        {
            _lastStartError = ex.Message;
            AppLog.Error("StartupAfterShownAsync failed", ex);
            UpdateStatusBar();
        }
    }

    private async Task CheckForUpdatesAfterStartupAsync()
    {
        try
        {
            // "A little after start": avoid racing UI creation and keep startup snappy.
            await Task.Delay(2500);
            if (IsDisposed)
                return;

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var latest = await UpdateChecker.TryGetLatestUpdateAsync(http, CancellationToken.None);
            if (latest is null)
                return;

            var current = UpdateChecker.GetCurrentVersion();
            if (!UpdateChecker.IsUpdateAvailable(current, latest.LatestVersion))
                return;

            _availableUpdate = latest;
            AppLog.Info($"Update available: current={current}, latest={latest.LatestVersion} ({latest.TagName})");

            if (IsDisposed)
                return;

            BeginInvoke(new Action(() =>
            {
                _statusUpdate.Visible = true;
                _statusUpdate.ToolTipText = $"Update available: {latest.TagName}";
            }));
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Update check failed: {ex.Message}");
        }
    }

    private async Task OnUpdateClickedAsync()
    {
        var info = _availableUpdate;
        if (info is null)
            return;

        var current = UpdateChecker.GetCurrentVersion();
        var msg = info.AssetDownloadUrl is null
            ? $"A newer version is available.\n\nCurrent: {current}\nLatest: {info.TagName}\n\nOpen the release page?"
            : $"A newer version is available.\n\nCurrent: {current}\nLatest: {info.TagName}\n\nUpdate now?";

        var result = MessageBox.Show(this, msg, "Update available", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
        if (result != DialogResult.OK)
            return;

        // If we can auto-update (zip/exe asset present), do it; otherwise just open the release page.
        if (info.AssetDownloadUrl is null)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = info.HtmlUrl.ToString(), UseShellExecute = true });
            }
            catch
            {
                // ignore
            }
            return;
        }

        await AppUpdater.TryUpdateToLatestAsync(info, this, CancellationToken.None);
    }

    private async Task<CopilotResponse> ExplainCurrentScreenAsync()
    {
        var ctx = BuildCopilotContext();
        var text = $"Explain what I'm seeing on the '{ctx.ActiveTab}' screen and what the next 1-2 actions should be.";
        return await RunAiCommandAsync(text);
    }

    private async Task<CopilotResponse> RunAiCommandAsync(string command)
    {
        if (!_aiSettings.Enabled)
        {
            return new CopilotResponse(
                AssistantText: "AI Copilot is disabled. Enable it in the AI (Experimental) section.",
                Clarification: null,
                ProposedActions: Array.Empty<CopilotAction>());
        }

        var apiKey = AiSettingsStore.UnprotectApiKey(_aiSettings.EncryptedApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new CopilotResponse(
                AssistantText: "No OpenAI API key is configured. Enter your key in AI (Experimental) to use natural-language commands.",
                Clarification: null,
                ProposedActions: Array.Empty<CopilotAction>());
        }

        var model = string.IsNullOrWhiteSpace(_aiSettings.Model) ? AiSettings.Default.Model : _aiSettings.Model;
        var ctx = BuildCopilotContext();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(18));
            return await _aiCopilot.HandleCommandAsync(apiKey, model, ctx, command, cts.Token);
        }
        catch (Exception ex)
        {
            return new CopilotResponse(
                AssistantText: ex.Message,
                Clarification: null,
                ProposedActions: Array.Empty<CopilotAction>());
        }
    }

    private CopilotContext BuildCopilotContext()
    {
        var activeTab = _tabs.SelectedTab?.Text ?? "(unknown)";
        var running = _router is not null;
        var gameSource = _simpleGameSourceCombo.SelectedItem as string;
        var secondarySource = _simpleSecondarySourceCombo.SelectedItem as string;
        var output = _simpleOutputCombo.SelectedItem as string;

        var outDev = _outputDeviceCombo.SelectedItem as string;
        var outputOk = _cachedOutputOk;

        var speakersEnabled = true;
        var shakerEnabled = true;
        if (_config.GroupRouting is { Length: 12 } gr)
        {
            speakersEnabled = gr[0] || gr[1];
            shakerEnabled = gr[2 * 2] || gr[2 * 2 + 1];
        }

        var gamePeak = Math.Max(_displayMusic[0], _displayMusic[1]);
        var secPeak = Math.Max(_displayShaker[0], _displayShaker[1]);
        var outLfe = _displayOut[3];
        var outMax = 0f;
        for (var i = 0; i < 8; i++)
            outMax = Math.Max(outMax, _displayOut[i]);

        return new CopilotContext(
            ActiveTab: activeTab,
            RouterRunning: running,
            GameSource: gameSource,
            SecondarySource: secondarySource,
            OutputDevice: string.IsNullOrWhiteSpace(outDev) ? output : outDev,
            OutputOk: string.IsNullOrWhiteSpace(outDev) ? true : outputOk,
            SpeakersEnabled: speakersEnabled,
            ShakerEnabled: shakerEnabled,
            MasterGainDb: _config.MasterGainDb,
            ShakerStrengthDb: _config.LfeGainDb,
            GamePeak: gamePeak,
            SecondaryPeak: secPeak,
            OutputLfePeak: outLfe,
                OutputPeakMax: outMax,
            HealthText: _statusHealth.Text ?? string.Empty);
    }

    private void ApplyCopilotActionsWithConfirmation(CopilotAction[] actions)
    {
        if (actions.Length == 0)
            return;

        var preview = new StringBuilder();
        preview.AppendLine("Apply these changes?");
        preview.AppendLine();
        foreach (var a in actions)
            preview.AppendLine($"- {DescribeCopilotAction(a)}");

        var result = MessageBox.Show(this, preview.ToString(), "Setup Assistant", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (result != DialogResult.OK)
            return;

        foreach (var a in actions)
            ExecuteCopilotAction(a);

        UpdateStatusBar();
    }

    private static string DescribeCopilotAction(CopilotAction a)
    {
        return a.Type switch
        {
            "set_output_device" => $"Set output device to '{a.StringValue}'",
            "set_game_source" => $"Set Game Source to '{a.StringValue}'",
            "apply_simple_preset" => $"Apply preset '{a.StringValue}'",
            "show_advanced_controls" => a.BoolValue == true ? "Show Advanced Controls" : "Hide Advanced Controls",
            "refresh_devices" => "Refresh device lists",
            "set_shaker_mode" => a.StringValue == "gamesOnly" ? "Shaker only for Game Source" : "Shaker for all sources",
            _ => a.Type
        };
    }

    private void ExecuteCopilotAction(CopilotAction a)
    {
        switch (a.Type)
        {
            case "refresh_devices":
                RefreshDeviceLists();
                break;

            case "set_output_device":
                if (!string.IsNullOrWhiteSpace(a.StringValue))
                {
                    _simpleOutputCombo.SelectedItem = a.StringValue;
                    _outputDeviceCombo.SelectedItem = a.StringValue;
                }
                break;

            case "set_game_source":
                if (!string.IsNullOrWhiteSpace(a.StringValue))
                {
                    _simpleGameSourceCombo.SelectedItem = a.StringValue;
                    _musicDeviceCombo.SelectedItem = a.StringValue;
                }
                break;

            case "set_secondary_source":
                if (!string.IsNullOrWhiteSpace(a.StringValue))
                {
                    _simpleSecondarySourceCombo.SelectedItem = a.StringValue;
                    _shakerDeviceCombo.SelectedItem = a.StringValue;
                }
                break;

            case "apply_simple_preset":
                var resolved = ResolveSimplePresetName(a.StringValue);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    _simplePresetCombo.SelectedItem = resolved;
                    ApplySimplePreset(resolved);
                }
                break;

            case "show_advanced_controls":
                if (a.BoolValue is bool b)
                {
                    _uiState = _uiState with { ShowAdvancedControls = b };
                    UiStateStore.Save(_uiState);
                    _simpleAdvancedToggle.Checked = b;
                    RebuildTabs(showAdvanced: b);
                }
                break;

            case "set_shaker_mode":
                ApplyShakerMode(a.StringValue);
                break;

            case "start_routing":
                StartRouter();
                break;

            case "stop_routing":
                StopRouter();
                break;

            case "set_channel_mute":
                if (a.IntValue is int ch && a.BoolValue is bool mute)
                {
                    ch = Math.Clamp(ch, 0, 7);
                    _channelMute[ch].Checked = mute;

                    // Best-effort: keep console toggles aligned (if present).
                    if (ch < _consoleOutMute.Length)
                        _consoleOutMute[ch].Checked = mute;
                }
                break;

            case "set_shaker_strength_db":
                if (a.FloatValue is float shakerDb)
                {
                    shakerDb = Math.Clamp(shakerDb, -24f, 12f);
                    _config.LfeGainDb = shakerDb;
                    _simpleShakerStrength.Value = (int)Math.Round(shakerDb * 10.0f);
                    _simpleShakerStrengthLabel.Text = $"{shakerDb:0.0} dB";
                }
                break;

            case "set_master_gain_db":
                if (a.FloatValue is float masterDb)
                {
                    masterDb = Math.Clamp(masterDb, -60f, 20f);
                    _config.MasterGainDb = masterDb;
                    _simpleMasterGain.Value = (int)Math.Round(masterDb * 10.0f);
                    _simpleMasterGainLabel.Text = $"{masterDb:0.0} dB";
                    _masterGainDb.Value = (decimal)masterDb;
                }
                break;
        }

        SaveConfigFromControls(showSavedDialog: false);
    }

    private string? ResolveSimplePresetName(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return null;

        // Allow assistant strings without emojis.
        var r = requested.Trim();

        foreach (var item in _simplePresetCombo.Items.Cast<object>())
        {
            var s = item as string;
            if (string.IsNullOrWhiteSpace(s)) continue;

            if (s.Contains(r, StringComparison.OrdinalIgnoreCase))
                return s;
        }

        return requested;
    }

    private void ApplyShakerMode(string? shakerMode)
    {
        // The config's GroupRouting is captured from the neon routing matrix UI.
        // Update the matrix so the change is visible and persists when saving.
        // Rows: Front, Center, LFE, Rear, Side, Reserved. Cols: A (Game Source), B (Secondary).
        var lfeRow = 2;
        var enableSecondaryLfe = !string.Equals(shakerMode, "gamesOnly", StringComparison.OrdinalIgnoreCase);

        _suppressConsoleSync = true;
        try
        {
            _consoleMatrix.Set(lfeRow, 0, true);
            _consoleMatrix.Set(lfeRow, 1, enableSecondaryLfe);
            _config.GroupRouting = ReadMatrixToConfig();
            _consoleMatrixDirty = true;
        }
        finally
        {
            _suppressConsoleSync = false;
        }
    }

    private void RebuildTabs(bool showAdvanced)
    {
        _tabs.TabPages.Clear();

        if (_simplePage is not null)
            _tabs.TabPages.Add(_simplePage);

        if (!showAdvanced)
        {
            _tabs.SelectedTab = _simplePage;
            return;
        }

        if (_devicesPage is not null) _tabs.TabPages.Add(_devicesPage);
        if (_diagnosticsPage is not null) _tabs.TabPages.Add(_diagnosticsPage);
        if (_dspPage is not null) _tabs.TabPages.Add(_dspPage);
        if (_routingPage is not null) _tabs.TabPages.Add(_routingPage);
        if (_channelsPage is not null) _tabs.TabPages.Add(_channelsPage);
        if (_metersPage is not null) _tabs.TabPages.Add(_metersPage);
        if (_calibrationPage is not null) _tabs.TabPages.Add(_calibrationPage);
    }

    private static void ApplyNeonTheme(Control root)
    {
        foreach (Control c in root.Controls)
        {
            // Don't restyle custom neon controls.
            if (c is NeonPanel or NeonMatrixControl or NeonMeter or NeonSlider or NeonToggleButton)
            {
                if (c.HasChildren) ApplyNeonTheme(c);
                continue;
            }

            switch (c)
            {
                case TabControl tc:
                    tc.BackColor = NeonTheme.BgPrimary;
                    tc.ForeColor = NeonTheme.TextSecondary;
                    break;

                case TabPage tp:
                    tp.BackColor = NeonTheme.BgPrimary;
                    tp.ForeColor = NeonTheme.TextPrimary;
                    break;

                case Panel or GroupBox:
                    if (c.BackColor == SystemColors.Control) c.BackColor = NeonTheme.BgPrimary;
                    if (c.ForeColor == SystemColors.ControlText) c.ForeColor = NeonTheme.TextPrimary;
                    break;

                case Label:
                    if (c.ForeColor == SystemColors.ControlText) c.ForeColor = NeonTheme.TextSecondary;
                    break;

                case TextBox tb:
                    tb.BorderStyle = BorderStyle.FixedSingle;
                    if (tb.BackColor == SystemColors.Window) tb.BackColor = NeonTheme.BgRaised;
                    if (tb.ForeColor == SystemColors.WindowText) tb.ForeColor = NeonTheme.TextPrimary;
                    break;

                case ComboBox cb:
                    cb.FlatStyle = FlatStyle.Flat;
                    if (cb.BackColor == SystemColors.Window) cb.BackColor = NeonTheme.BgRaised;
                    if (cb.ForeColor == SystemColors.WindowText) cb.ForeColor = NeonTheme.TextPrimary;
                    break;

                case NumericUpDown nud:
                    if (nud.BackColor == SystemColors.Window) nud.BackColor = NeonTheme.BgRaised;
                    if (nud.ForeColor == SystemColors.WindowText) nud.ForeColor = NeonTheme.TextPrimary;
                    break;

                case Button b:
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderSize = 1;
                    b.FlatAppearance.BorderColor = Color.FromArgb(140, NeonTheme.NeonPurple);
                    if (b.BackColor == SystemColors.Control) b.BackColor = NeonTheme.BgRaised;
                    if (b.ForeColor == SystemColors.ControlText) b.ForeColor = NeonTheme.TextPrimary;
                    break;

                case CheckBox cbx:
                    if (cbx.ForeColor == SystemColors.ControlText) cbx.ForeColor = NeonTheme.TextPrimary;
                    break;

                case ListBox lb:
                    if (lb.BackColor == SystemColors.Window) lb.BackColor = NeonTheme.BgRaised;
                    if (lb.ForeColor == SystemColors.WindowText) lb.ForeColor = NeonTheme.TextPrimary;
                    break;

                case DataGridView dgv:
                    dgv.EnableHeadersVisualStyles = false;
                    dgv.BackgroundColor = NeonTheme.BgPrimary;
                    dgv.GridColor = Color.FromArgb(50, 255, 255, 255);
                    dgv.DefaultCellStyle.BackColor = NeonTheme.BgRaised;
                    dgv.DefaultCellStyle.ForeColor = NeonTheme.TextPrimary;
                    dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(80, NeonTheme.NeonCyan);
                    dgv.DefaultCellStyle.SelectionForeColor = NeonTheme.TextPrimary;
                    dgv.ColumnHeadersDefaultCellStyle.BackColor = NeonTheme.BgPanel;
                    dgv.ColumnHeadersDefaultCellStyle.ForeColor = NeonTheme.TextSecondary;
                    break;
            }

            if (c.HasChildren) ApplyNeonTheme(c);
        }
    }

    private TabPage BuildRoutingTab()
    {
        var page = new TabPage("Routing")
        {
            BackColor = NeonTheme.BgPrimary,
            ForeColor = NeonTheme.TextPrimary
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));

        var inputs = BuildConsoleInputsPanel();
        var matrix = BuildConsoleMatrixPanel();
        var outputs = BuildConsoleOutputsPanel();

        root.Controls.Add(inputs, 0, 0);
        root.Controls.Add(matrix, 1, 0);
        root.Controls.Add(outputs, 2, 0);

        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildSimpleTab()
    {
        var page = new TabPage("Simple")
        {
            BackColor = NeonTheme.BgPrimary,
            ForeColor = NeonTheme.TextPrimary
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // flow
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // header
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // devices
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // presets
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // sliders
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // buttons + advanced
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // hint

        _simpleFlow.Margin = new Padding(0, 0, 0, 10);
        root.Controls.Add(_simpleFlow, 0, 0);

        var headerRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var title = new Label { Text = "Quick Start", AutoSize = true, Font = NeonTheme.CreateBaseFont(20, FontStyle.Bold), ForeColor = NeonTheme.TextPrimary };
        var subtitle = new Label { Text = "  (2-minute setup)", AutoSize = true, Font = NeonTheme.CreateBaseFont(12), ForeColor = NeonTheme.TextMuted, Padding = new Padding(0, 7, 0, 0) };
        var help = new Button { Text = "?", Width = 34, Height = 28, Margin = new Padding(14, 4, 0, 0) };
        help.Click += (_, _) =>
        {
            MessageBox.Show(this,
                "What do I do next?\n\n1) Select your Game Audio Source\n2) Select Output Device (CM6206 7.1)\n3) Pick a preset\n4) Press Start Routing\n\nIf you hear no shaker: increase Bass Shaker strength.",
                "Quick Start",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        };
        headerRow.Controls.Add(title);
        headerRow.Controls.Add(subtitle);
        headerRow.Controls.Add(help);
        root.Controls.Add(headerRow, 0, 1);

        var devicesPanel = new NeonPanel { Dock = DockStyle.Top, NoiseOverlay = true, Padding = new Padding(14), AutoSize = true };
        var devicesLayout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 6 };
        devicesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        devicesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        devicesLayout.Controls.Add(new Label { Text = "Game audio source", AutoSize = true, ForeColor = NeonTheme.TextPrimary }, 0, 0);
        devicesLayout.Controls.Add(_simpleGameSourceCombo, 1, 0);
        devicesLayout.Controls.Add(new Label { Text = "Capture system audio", AutoSize = true, ForeColor = NeonTheme.TextMuted, Font = NeonTheme.CreateMonoFont(10) }, 1, 1);

        devicesLayout.Controls.Add(new Label { Text = "Music / secondary source", AutoSize = true, ForeColor = NeonTheme.TextPrimary }, 0, 2);
        devicesLayout.Controls.Add(_simpleSecondarySourceCombo, 1, 2);
        devicesLayout.Controls.Add(new Label { Text = "Optional second source", AutoSize = true, ForeColor = NeonTheme.TextMuted, Font = NeonTheme.CreateMonoFont(10) }, 1, 3);

        devicesLayout.Controls.Add(new Label { Text = "Output device", AutoSize = true, ForeColor = NeonTheme.TextPrimary }, 0, 4);
        devicesLayout.Controls.Add(_simpleOutputCombo, 1, 4);
        devicesLayout.Controls.Add(new Label { Text = "Audio device", AutoSize = true, ForeColor = NeonTheme.TextMuted, Font = NeonTheme.CreateMonoFont(10) }, 1, 5);

        devicesPanel.Controls.Add(devicesLayout);
        root.Controls.Add(devicesPanel, 0, 2);

        _toolTip.SetToolTip(_simpleGameSourceCombo, "Choose where your game audio plays (we capture that audio).");
        _toolTip.SetToolTip(_simpleSecondarySourceCombo, "Optional: a second source (music player, browser, etc).");
        _toolTip.SetToolTip(_simpleOutputCombo, "Choose where the mixed audio will play (usually CM6206 7.1).");

        var presetPanel = new NeonPanel { Dock = DockStyle.Top, NoiseOverlay = true, Padding = new Padding(14), AutoSize = true };
        var presetRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        presetRow.Controls.Add(new Label { Text = "Preset", AutoSize = true, ForeColor = NeonTheme.TextPrimary, Padding = new Padding(0, 6, 8, 0) });
        presetRow.Controls.Add(_simplePresetCombo);
        presetRow.Controls.Add(new Label { Text = "  ", AutoSize = true });
        presetRow.Controls.Add(_simplePresetSummary);
        presetPanel.Controls.Add(presetRow);
        root.Controls.Add(presetPanel, 0, 3);

        _toolTip.SetToolTip(_simplePresetCombo, "Presets configure speakers vs bass shaker routing.");

        var slidersPanel = new NeonPanel { Dock = DockStyle.Top, NoiseOverlay = true, Padding = new Padding(14), AutoSize = true };
        var slidersLayout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 4 };
        slidersLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        slidersLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        slidersLayout.Controls.Add(new Label { Text = "Master volume", AutoSize = true, ForeColor = NeonTheme.TextPrimary }, 0, 0);
        slidersLayout.Controls.Add(BuildGainRow(_simpleMasterGain, _simpleMasterGainLabel, onChanged: v =>
        {
            if (_suppressConsoleSync) return;
            _suppressConsoleSync = true;
            try
            {
                _masterGainDb.Value = (decimal)(v / 10.0);
                _simpleMasterGainLabel.Text = $"{v / 10.0:0.0} dB";
                if (_router is not null) _config.MasterGainDb = (float)_masterGainDb.Value;
            }
            finally { _suppressConsoleSync = false; }
        }), 1, 0);
        slidersLayout.Controls.Add(new Label { Text = "Overall loudness", AutoSize = true, ForeColor = NeonTheme.TextMuted, Font = NeonTheme.CreateMonoFont(10) }, 1, 1);

        slidersLayout.Controls.Add(new Label { Text = "Bass shaker strength", AutoSize = true, ForeColor = NeonTheme.TextPrimary }, 0, 2);
        slidersLayout.Controls.Add(BuildGainRow(_simpleShakerStrength, _simpleShakerStrengthLabel, onChanged: v =>
        {
            if (_suppressConsoleSync) return;
            _suppressConsoleSync = true;
            try
            {
                var db = v / 10.0f;
                _simpleShakerStrengthLabel.Text = $"{db:0.0} dB";
                if (_router is not null) _config.LfeGainDb = db;
            }
            finally { _suppressConsoleSync = false; }
        }), 1, 2);
        slidersLayout.Controls.Add(new Label { Text = "How strong the shaker feels", AutoSize = true, ForeColor = NeonTheme.TextMuted, Font = NeonTheme.CreateMonoFont(10) }, 1, 3);

        slidersPanel.Controls.Add(slidersLayout);
        root.Controls.Add(slidersPanel, 0, 4);

        _toolTip.SetToolTip(_simpleMasterGain, "Increase or reduce the overall volume.");
        _toolTip.SetToolTip(_simpleShakerStrength, "Increase or reduce how strong the bass shaker is.");

        var buttonsPanel = new NeonPanel { Dock = DockStyle.Top, NoiseOverlay = true, Padding = new Padding(14), AutoSize = true };
        var buttonsRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        _simpleStartButton.Font = NeonTheme.CreateBaseFont(14, FontStyle.Bold);
        _simpleStopButton.Font = NeonTheme.CreateBaseFont(14, FontStyle.Bold);
        _simpleStartButton.Click += (_, _) => StartRouter();
        _simpleStopButton.Click += (_, _) => StopRouter();

        _simpleAdvancedToggle.Checked = _uiState.ShowAdvancedControls;
        _simpleAdvancedToggle.CheckedChanged += (_, _) =>
        {
            _uiState = _uiState with { ShowAdvancedControls = _simpleAdvancedToggle.Checked, HasSeenSimpleMode = true };
            UiStateStore.Save(_uiState);
            RebuildTabs(showAdvanced: _uiState.ShowAdvancedControls);
            UpdateStatusBar();
        };

        _toolTip.SetToolTip(_simpleAdvancedToggle, "Show all controls (routing matrix, per-speaker trim, calibration, profiles).");

        buttonsRow.Controls.Add(_simpleStartButton);
        buttonsRow.Controls.Add(_simpleStopButton);
        buttonsRow.Controls.Add(new Label { Text = "   ", AutoSize = true, Padding = new Padding(0, 12, 0, 0) });
        buttonsRow.Controls.Add(_simpleAdvancedToggle);
        buttonsRow.Controls.Add(new Label { Text = "   ", AutoSize = true, Padding = new Padding(0, 12, 0, 0) });
        buttonsRow.Controls.Add(_simpleStatus);
        buttonsPanel.Controls.Add(buttonsRow);
        root.Controls.Add(buttonsPanel, 0, 5);

        var hintPanel = new Panel { Dock = DockStyle.Fill };
        _simpleNextHint.Dock = DockStyle.Bottom;
        hintPanel.Controls.Add(_simpleNextHint);
        root.Controls.Add(hintPanel, 0, 6);

        // Wire simple dropdowns to advanced dropdowns + update status.
        _simpleGameSourceCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressConsoleSync) return;
            _suppressConsoleSync = true;
            try
            {
                _musicDeviceCombo.SelectedItem = _simpleGameSourceCombo.SelectedItem;
            }
            finally { _suppressConsoleSync = false; }
            UpdateStatusBar();
        };
        _simpleSecondarySourceCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressConsoleSync) return;
            _suppressConsoleSync = true;
            try
            {
                _shakerDeviceCombo.SelectedItem = _simpleSecondarySourceCombo.SelectedItem;
            }
            finally { _suppressConsoleSync = false; }
            UpdateStatusBar();
        };
        _simpleOutputCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressConsoleSync) return;
            _suppressConsoleSync = true;
            try
            {
                _outputDeviceCombo.SelectedItem = _simpleOutputCombo.SelectedItem;
            }
            finally { _suppressConsoleSync = false; }
            UpdateStatusBar();
        };

        // Simple presets
        _simplePresetCombo.Items.Clear();
        _simplePresetCombo.Items.Add("Game + Bass Shaker (Recommended)");
        _simplePresetCombo.Items.Add("Music Clean");
        _simplePresetCombo.Items.Add("Game Only");
        _simplePresetCombo.Items.Add("Shaker Only (No Speakers)");
        _simplePresetCombo.Items.Add("Flat / Debug");
        _simplePresetCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressConsoleSync) return;
            ApplySimplePreset(_simplePresetCombo.SelectedItem as string);
        };
        if (_simplePresetCombo.Items.Count > 0)
            _simplePresetCombo.SelectedIndex = 0;

        page.Controls.Add(root);

        // Mark that the user has seen the Simple Mode landing.
        if (!_uiState.HasSeenSimpleMode)
        {
            _uiState = _uiState with { HasSeenSimpleMode = true };
            UiStateStore.Save(_uiState);
        }

        return page;
    }

    private void ApplySimplePreset(string? presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName))
            return;

        bool[,] matrix = new bool[6, 2];

        // Rows: Front, Center, LFE, Rear, Side, Reserved. Cols: A=Game, B=Secondary.
        if (presetName.Contains("Game + Bass Shaker", StringComparison.OrdinalIgnoreCase))
        {
            matrix[0, 0] = true; // Front from A
            matrix[2, 0] = true; // LFE from A
            _simplePresetSummary.Text = "Speakers: Game audio • Shaker: Game bass";
        }
        else if (presetName.Contains("Music Clean", StringComparison.OrdinalIgnoreCase))
        {
            matrix[0, 1] = true; // Front from B
            _simplePresetSummary.Text = "Speakers: Music • Shaker: Off";
        }
        else if (presetName.Contains("Game Only", StringComparison.OrdinalIgnoreCase))
        {
            matrix[0, 0] = true; // Front from A
            _simplePresetSummary.Text = "Speakers: Game audio • Shaker: Off";
        }
        else if (presetName.Contains("Shaker Only", StringComparison.OrdinalIgnoreCase))
        {
            matrix[2, 0] = true; // LFE from A
            _simplePresetSummary.Text = "Speakers: Off • Shaker: Game bass";
        }
        else
        {
            // Flat/debug: remove matrix override, fall back to mixing mode.
            _simplePresetSummary.Text = "No routing override (advanced controls apply)";

            _suppressConsoleSync = true;
            try
            {
                _consoleMatrixDirty = false;
                _config.GroupRouting = null;
            }
            finally { _suppressConsoleSync = false; }

            UpdateStatusBar();
            return;
        }

        _suppressConsoleSync = true;
        try
        {
            _consoleMatrixDirty = true;
            _consoleMatrix.SetAll(matrix);
            _config.GroupRouting = ReadMatrixToConfig();
        }
        finally { _suppressConsoleSync = false; }

        UpdateStatusBar();
    }

    private Control BuildConsoleInputsPanel()
    {
        var panel = new NeonPanel { Dock = DockStyle.Fill, NoiseOverlay = true };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            AutoSize = true
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(BuildHeader("Inputs"), 0, 0);

        var meterRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        meterRow.Controls.Add(BuildMeterLabel("A"));
        meterRow.Controls.Add(_consoleMeterA);
        meterRow.Controls.Add(new Label { Width = 18 });
        meterRow.Controls.Add(BuildMeterLabel("B"));
        meterRow.Controls.Add(_consoleMeterB);
        layout.Controls.Add(meterRow, 0, 1);

        layout.Controls.Add(new Label { Text = "Gain A (Music)", AutoSize = true, ForeColor = NeonTheme.TextSecondary }, 0, 2);
        layout.Controls.Add(BuildGainRow(_consoleMusicGain, _consoleMusicGainLabel, onChanged: v =>
        {
            if (_suppressConsoleSync) return;
            _suppressConsoleSync = true;
            try
            {
                _musicGainDb.Value = (decimal)(v / 10.0);
                _consoleMusicGainLabel.Text = $"{v / 10.0:0.0} dB";
                if (_router is not null) { _config.MusicGainDb = (float)_musicGainDb.Value; }
            }
            finally { _suppressConsoleSync = false; }
        }), 0, 3);

        layout.Controls.Add(new Label { Text = "Gain B (Shaker)", AutoSize = true, ForeColor = NeonTheme.TextSecondary }, 0, 4);
        layout.Controls.Add(BuildGainRow(_consoleShakerGain, _consoleShakerGainLabel, onChanged: v =>
        {
            if (_suppressConsoleSync) return;
            _suppressConsoleSync = true;
            try
            {
                _shakerGainDb.Value = (decimal)(v / 10.0);
                _consoleShakerGainLabel.Text = $"{v / 10.0:0.0} dB";
                if (_router is not null) { _config.ShakerGainDb = (float)_shakerGainDb.Value; }
            }
            finally { _suppressConsoleSync = false; }
        }), 0, 5);

        layout.Controls.Add(new Label { Text = "Input mode", AutoSize = true, ForeColor = NeonTheme.TextSecondary }, 0, 6);
        _consoleInputMode.Items.Clear();
        _consoleInputMode.Items.Add("A");
        _consoleInputMode.Items.Add("B");
        _consoleInputMode.Items.Add("A+B");
        _consoleInputMode.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressConsoleSync) return;
            ApplyConsoleInputModePreset();
        };
        layout.Controls.Add(_consoleInputMode, 0, 7);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildConsoleMatrixPanel()
    {
        var panel = new NeonPanel { Dock = DockStyle.Fill, NoiseOverlay = true };

        _consoleMatrix.RowLabels = new[] { "Front", "Center", "LFE", "Rear", "Side", "Reserved" };
        _consoleMatrix.ColLabels = new[] { "A", "B" };
        _consoleMatrix.Dock = DockStyle.Fill;
        _consoleMatrix.BackColor = NeonTheme.BgPanel;
        _consoleMatrix.CellsChanged += (_, _) =>
        {
            if (_suppressConsoleSync) return;
            _consoleMatrixDirty = true;
            _config.GroupRouting = ReadMatrixToConfig();
            UpdateStatusBar();
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(BuildHeader("Routing"), 0, 0);
        layout.Controls.Add(_consoleMatrix, 0, 1);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildConsoleOutputsPanel()
    {
        var panel = new NeonPanel { Dock = DockStyle.Fill, NoiseOverlay = true };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildHeader("Outputs"), 0, 0);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 5,
            RowCount = 9
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46)); // label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // meter
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // gain
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42)); // mute
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42)); // solo

        layout.Controls.Add(new Label { Text = "Ch", AutoSize = true, ForeColor = NeonTheme.TextSecondary }, 0, 0);
        layout.Controls.Add(new Label { Text = "Lvl", AutoSize = true, ForeColor = NeonTheme.TextSecondary }, 1, 0);
        layout.Controls.Add(new Label { Text = "Gain", AutoSize = true, ForeColor = NeonTheme.TextSecondary }, 2, 0);
        layout.Controls.Add(new Label { Text = "M", AutoSize = true, ForeColor = NeonTheme.TextSecondary }, 3, 0);
        layout.Controls.Add(new Label { Text = "S", AutoSize = true, ForeColor = NeonTheme.TextSecondary }, 4, 0);

        for (var i = 0; i < 8; i++)
        {
            var ch = i;
            _consoleOutMeters[i] = new NeonMeter { Vertical = false, Width = 110, Height = 14 };

            _consoleOutGain[i] = new NeonSlider { Minimum = -240, Maximum = 120, Value = 0, Width = 160 };
            _consoleOutGainLabel[i] = new Label { Text = "0.0 dB", AutoSize = true, ForeColor = NeonTheme.TextSecondary, Font = NeonTheme.CreateMonoFont(12) };

            var gainRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            gainRow.Controls.Add(_consoleOutGain[i]);
            gainRow.Controls.Add(_consoleOutGainLabel[i]);

            _consoleOutMute[i] = CreateToggle("M");
            _consoleOutSolo[i] = CreateToggle("S");

            _consoleOutGain[i].ValueChanged += (_, _) =>
            {
                if (_suppressConsoleSync) return;
                _suppressConsoleSync = true;
                try
                {
                    _consoleOutGainLabel[ch].Text = $"{_consoleOutGain[ch].Value / 10.0:0.0} dB";
                    _channelSliders[ch].Value = _consoleOutGain[ch].Value;
                    _channelLabels[ch].Text = _consoleOutGainLabel[ch].Text;
                }
                finally { _suppressConsoleSync = false; }
            };

            _consoleOutMute[i].CheckedChanged += (_, _) =>
            {
                if (_suppressConsoleSync) return;
                _suppressConsoleSync = true;
                try { _channelMute[ch].Checked = _consoleOutMute[ch].Checked; }
                finally { _suppressConsoleSync = false; }
            };
            _consoleOutSolo[i].CheckedChanged += (_, _) =>
            {
                if (_suppressConsoleSync) return;
                _suppressConsoleSync = true;
                try { _channelSolo[ch].Checked = _consoleOutSolo[ch].Checked; }
                finally { _suppressConsoleSync = false; }
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.Controls.Add(new Label { Text = ShortName(i), AutoSize = true, ForeColor = NeonTheme.TextPrimary }, 0, i + 1);
            layout.Controls.Add(_consoleOutMeters[i], 1, i + 1);
            layout.Controls.Add(gainRow, 2, i + 1);
            layout.Controls.Add(_consoleOutMute[i], 3, i + 1);
            layout.Controls.Add(_consoleOutSolo[i], 4, i + 1);
        }

        scroll.Controls.Add(layout);
        root.Controls.Add(scroll, 0, 1);
        panel.Controls.Add(root);
        return panel;
    }

    private static Control BuildHeader(string title)
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var glyph = new Label { Text = "∿", AutoSize = true, ForeColor = NeonTheme.NeonCyan, Font = NeonTheme.CreateBaseFont(18, FontStyle.Bold) };
        var text = new Label { Text = "  " + title, AutoSize = true, ForeColor = NeonTheme.TextPrimary, Font = NeonTheme.CreateBaseFont(18, FontStyle.Bold) };
        row.Controls.Add(glyph);
        row.Controls.Add(text);
        return row;
    }

    private static Label BuildMeterLabel(string text)
        => new() { Text = text, AutoSize = true, ForeColor = NeonTheme.TextSecondary, Padding = new Padding(0, 68, 6, 0) };

    private static Control BuildGainRow(NeonSlider slider, Label valueLabel, Action<int> onChanged)
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        slider.ValueChanged += (_, _) => onChanged(slider.Value);
        row.Controls.Add(slider);
        valueLabel.Font = NeonTheme.CreateMonoFont(12);
        row.Controls.Add(valueLabel);
        return row;
    }

    private static NeonToggleButton CreateToggle(string text)
    {
        return new NeonToggleButton { Text = text, Width = 32, Height = 22 };
    }

    private TabPage BuildMetersTab()
    {
        var page = new TabPage("Meters") { BackColor = NeonTheme.BgPrimary, ForeColor = NeonTheme.TextPrimary };

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

        // SÖNDBÖUND console meters
        _consoleMeterA.Peak = Math.Max(_displayMusic[0], _displayMusic[1]);
        _consoleMeterB.Peak = Math.Max(_displayShaker[0], _displayShaker[1]);
        for (var ch = 0; ch < 8; ch++)
        {
            var m = _consoleOutMeters[ch];
            if (m is not null)
                m.Peak = _displayOut[ch];
        }
    }

    private void ClearMeters()
    {
        Array.Clear(_displayMusic);
        Array.Clear(_displayShaker);
        Array.Clear(_displayOut);

        UpdateMeterUi(_musicMeters, _musicMeterLabels, _musicClipLabels, _displayMusic);
        UpdateMeterUi(_shakerMeters, _shakerMeterLabels, _shakerClipLabels, _displayShaker);
        UpdateMeterUi(_outputMeters, _outputMeterLabels, _outputClipLabels, _displayOut);

        _consoleMeterA.Peak = 0f;
        _consoleMeterB.Peak = 0f;
        for (var ch = 0; ch < 8; ch++)
        {
            var m = _consoleOutMeters[ch];
            if (m is not null)
                m.Peak = 0f;
        }
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
        var page = new TabPage("Devices") { BackColor = NeonTheme.BgPrimary, ForeColor = NeonTheme.TextPrimary };

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

        layout.Controls.Add(new Label { Text = "Game audio source", AutoSize = true }, 0, 0);
        layout.Controls.Add(_musicDeviceCombo, 1, 0);

        layout.Controls.Add(new Label { Text = "Music / secondary source", AutoSize = true }, 0, 1);
        layout.Controls.Add(_shakerDeviceCombo, 1, 1);

        layout.Controls.Add(new Label { Text = "Output device", AutoSize = true }, 0, 2);
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
        profileRow.Controls.Add(_presetMovieButton);
        profileRow.Controls.Add(_presetMusicButton);
        profileRow.Controls.Add(_presetGameButton);
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
        _presetMovieButton.Click += (_, _) => LoadNamedProfileIntoUi("Movie Mode");
        _presetMusicButton.Click += (_, _) => LoadNamedProfileIntoUi("Music Mode");
        _presetGameButton.Click += (_, _) => LoadNamedProfileIntoUi("Game Mode");
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
        var page = new TabPage("Diagnostics") { BackColor = NeonTheme.BgPrimary, ForeColor = NeonTheme.TextPrimary };

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

                sb.AppendLine();
                sb.AppendLine("Decoded:");
                sb.AppendLine(Cm6206RegisterDecoder.Decode(regs));

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

        RefreshStatusPresetMenu();
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

    private void LoadNamedProfileIntoUi(string profileName)
    {
        try
        {
            RefreshProfilesCombo();

            var match = _profileCombo.Items
                .OfType<string>()
                .FirstOrDefault(s => string.Equals(s, profileName, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                MessageBox.Show(this, $"Preset profile '{profileName}' not found.", "Preset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _profileCombo.SelectedItem = match;
            LoadSelectedProfileIntoUi();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Preset load failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
        var page = new TabPage("DSP") { BackColor = NeonTheme.BgPrimary, ForeColor = NeonTheme.TextPrimary };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 16,
            Padding = new Padding(12),
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Music gain (dB)", AutoSize = true }, 0, 0);
        layout.Controls.Add(_musicGainDb, 1, 0);

        layout.Controls.Add(new Label { Text = "Shaker gain (dB)", AutoSize = true }, 0, 1);
        layout.Controls.Add(_shakerGainDb, 1, 1);

        layout.Controls.Add(new Label { Text = "Master output gain (dB)", AutoSize = true }, 0, 2);
        layout.Controls.Add(_masterGainDb, 1, 2);

        layout.Controls.Add(new Label { Text = "Shaker high-pass (Hz)", AutoSize = true }, 0, 3);
        layout.Controls.Add(_hpHz, 1, 3);

        layout.Controls.Add(new Label { Text = "Shaker low-pass (Hz)", AutoSize = true }, 0, 4);
        layout.Controls.Add(_lpHz, 1, 4);

        layout.Controls.Add(new Label { Text = "Mixing mode", AutoSize = true }, 0, 5);
        _mixingModeCombo.Width = 360;
        _mixingModeCombo.Items.Clear();
        _mixingModeCombo.Items.Add("A+B (Front = Music + Shaker)");
        _mixingModeCombo.Items.Add("A only (Music only)");
        _mixingModeCombo.Items.Add("B only (Shaker only)");
        _mixingModeCombo.Items.Add("Priority switch (prefer Music)");
        _mixingModeCombo.Items.Add("Priority switch (prefer Shaker)");
        _mixingModeCombo.Items.Add("Dedicated (Front = Music only; Shaker = Rear/Side/LFE)");
        layout.Controls.Add(_mixingModeCombo, 1, 5);

        // Optional music filters
        layout.Controls.Add(new Label { Text = "Music high-pass (Hz)", AutoSize = true }, 0, 11);
        var mhp = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        mhp.Controls.Add(_musicHpEnable);
        mhp.Controls.Add(_musicHpHz);
        layout.Controls.Add(mhp, 1, 11);

        layout.Controls.Add(new Label { Text = "Music low-pass (Hz)", AutoSize = true }, 0, 12);
        var mlp = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        mlp.Controls.Add(_musicLpEnable);
        mlp.Controls.Add(_musicLpHz);
        layout.Controls.Add(mlp, 1, 12);

        layout.Controls.Add(new Label { Text = "Latency (ms)", AutoSize = true }, 0, 6);
        layout.Controls.Add(_latencyMs, 1, 6);

        layout.Controls.Add(new Label { Text = "Preferred sample rate (Hz)", AutoSize = true }, 0, 7);
        _sampleRateCombo.Width = 140;
        _sampleRateCombo.Items.Clear();
        foreach (var sr in OutputFormatNegotiator.CandidateSampleRates)
            _sampleRateCombo.Items.Add(sr);
        layout.Controls.Add(_sampleRateCombo, 1, 7);

        layout.Controls.Add(_useCenter, 1, 8);

        layout.Controls.Add(_useExclusiveMode, 1, 9);

        layout.Controls.Add(new Label { Text = "Output format helper", AutoSize = true }, 0, 10);

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
        layout.Controls.Add(helperGroup, 1, 10);

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
        if (_suppressFormatUpdate)
            return;

        var devName = _outputDeviceCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(devName))
        {
            _mixFormatLabel.Text = "";
            _effectiveFormatLabel.Text = "";
            _formatWarningLabel.Text = "";
            return;
        }

        _sampleRateCombo.Enabled = _useExclusiveMode.Checked;

        var temp = _config.Clone();
        temp.OutputRenderDevice = devName;
        temp.UseExclusiveMode = _useExclusiveMode.Checked;
        temp.SampleRate = GetSelectedSampleRate();

        var requestId = Interlocked.Increment(ref _formatInfoRequestId);
        _ = UpdateFormatInfoAsync(requestId, devName, temp);
    }

    private async Task UpdateFormatInfoAsync(int requestId, string devName, RouterConfig temp)
    {
        try
        {
            var probeTask = Task.Run(() =>
            {
                using var outputDevice = DeviceHelper.GetRenderDeviceByFriendlyName(devName);
                var mix = outputDevice.AudioClient.MixFormat;
                var negotiation = OutputFormatNegotiator.Negotiate(temp, outputDevice);
                return (mix, negotiation);
            });

            var completed = await Task.WhenAny(probeTask, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(true);
            if (completed != probeTask)
                throw new TimeoutException("Timed out while probing the output device format.");

            var (mix, negotiation) = await probeTask.ConfigureAwait(true);
            if (IsDisposed)
                return;

            BeginInvoke(new Action(() =>
            {
                if (requestId != _formatInfoRequestId)
                    return;

                _mixFormatLabel.Text = $"Windows mix: {mix.SampleRate} Hz, {mix.Channels}ch ({mix.Encoding})";
                _effectiveFormatLabel.Text = $"Effective output: 7.1 float @ {negotiation.EffectiveConfig.SampleRate} Hz ({(temp.UseExclusiveMode ? "Exclusive" : "Shared")})";
                _formatWarningLabel.Text = negotiation.Warning ?? "";
            }));
        }
        catch (Exception ex)
        {
            AppLog.Warn($"UpdateFormatInfo failed: {ex.Message}");
            if (IsDisposed)
                return;

            BeginInvoke(new Action(() =>
            {
                if (requestId != _formatInfoRequestId)
                    return;

                _effectiveFormatLabel.Text = "";
                _formatWarningLabel.Text = ex.Message;
            }));
        }
    }

    private TabPage BuildChannelsTab()
    {
        var page = new TabPage("Channels") { BackColor = NeonTheme.BgPrimary, ForeColor = NeonTheme.TextPrimary };

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

        UpdateRoutingGrid();
    }

    private static string FormatDb(float db)
    {
        var sign = db >= 0 ? "+" : "";
        return $"{sign}{db:0.0} dB";
    }

    private RouterConfig BuildConfigSnapshotFromControls()
    {
        var temp = _config.Clone();

        temp.MusicGainDb = (float)_musicGainDb.Value;
        temp.ShakerGainDb = (float)_shakerGainDb.Value;
        temp.MasterGainDb = (float)_masterGainDb.Value;

        temp.MusicHighPassHz = _musicHpEnable.Checked ? (float)_musicHpHz.Value : null;
        temp.MusicLowPassHz = _musicLpEnable.Checked ? (float)_musicLpHz.Value : null;

        temp.ShakerHighPassHz = (float)_hpHz.Value;
        temp.ShakerLowPassHz = (float)_lpHz.Value;

        temp.MixingMode = _mixingModeCombo.SelectedIndex switch
        {
            1 => "MusicOnly",
            2 => "ShakerOnly",
            3 => "PriorityMusic",
            4 => "PriorityShaker",
            5 => "Dedicated",
            _ => "FrontBoth"
        };

        temp.UseCenterChannel = _useCenter.Checked;

        // Channel state from controls (even if not saved yet)
        var channel = new float[8];
        var map = new int[8];
        var mute = new bool[8];
        var solo = new bool[8];
        var invert = new bool[8];
        for (var i = 0; i < 8; i++)
        {
            channel[i] = _channelSliders[i].Value / 10.0f;
            map[i] = _channelMap[i].SelectedIndex < 0 ? i : _channelMap[i].SelectedIndex;
            mute[i] = _channelMute[i].Checked;
            solo[i] = _channelSolo[i].Checked;
            invert[i] = _channelInvert[i].Checked;
        }
        temp.ChannelGainsDb = channel;
        temp.OutputChannelMap = map;
        temp.ChannelMute = mute;
        temp.ChannelSolo = solo;
        temp.ChannelInvert = invert;

        // Routing matrix override (always captured from console UI)
        temp.GroupRouting = ReadMatrixToConfig();

        return temp;
    }

    private bool[] ReadMatrixToConfig()
    {
        var arr = new bool[12];
        for (var r = 0; r < 6; r++)
        {
            for (var c = 0; c < 2; c++)
            {
                arr[r * 2 + c] = _consoleMatrix.Get(r, c);
            }
        }
        return arr;
    }

    private void ApplyConsoleInputModePreset()
    {
        // A/B/A+B presets that match the spec and sane defaults.
        // Rows: Front, Center, LFE, Rear, Side, Reserved.
        // Cols: A (Music), B (Shaker).
        var mode = _consoleInputMode.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(mode)) return;

        var values = new bool[6, 2];
        // baseline: everything off

        if (mode == "A")
        {
            // Music to fronts only.
            values[0, 0] = true;
        }
        else if (mode == "B")
        {
            // Shaker everywhere (including fronts).
            for (var r = 0; r < 5; r++)
                values[r, 1] = true;
        }
        else // A+B
        {
            // Front: A+B, everything else: B.
            values[0, 0] = true;
            values[0, 1] = true;
            for (var r = 1; r < 5; r++)
                values[r, 1] = true;
        }

        _suppressConsoleSync = true;
        try
        {
            _consoleMatrix.SetAll(values);
            _config.GroupRouting = ReadMatrixToConfig();
            _consoleMatrixDirty = true;
        }
        finally
        {
            _suppressConsoleSync = false;
        }
    }

    private void UpdateRoutingGrid()
    {
        if (_routingGrid.IsDisposed) return;

        var temp = BuildConfigSnapshotFromControls();

        _routingGrid.SuspendLayout();
        try
        {
            _routingGrid.Rows.Clear();

            var anySolo = temp.ChannelSolo?.Any(s => s) == true;
            for (var outCh = 0; outCh < 8; outCh++)
            {
                var src = temp.OutputChannelMap?[outCh] ?? outCh;
                src = Math.Clamp(src, 0, 7);

                var inputs = DescribeInputsForRawChannel(src, temp);
                var rawDef = DescribeRawChannel(src, temp);
                var outGainDb = temp.ChannelGainsDb?[outCh] ?? 0f;
                var gains = $"Master {FormatDb(temp.MasterGainDb)}, Out {FormatDb(outGainDb)}";

                var muted = temp.ChannelMute?[outCh] == true;
                var solo = temp.ChannelSolo?[outCh] == true;
                var invert = temp.ChannelInvert?[outCh] == true;

                // If any solo is active and this channel isn't soloed, it'll effectively be muted.
                if (anySolo && !solo)
                    rawDef += " (solo-gated)";

                _routingGrid.Rows.Add(
                    ShortName(outCh),
                    inputs,
                    $"{ShortName(outCh)} <- {ShortName(src)}",
                    rawDef,
                    gains,
                    muted,
                    solo,
                    invert);
            }
        }
        finally
        {
            _routingGrid.ResumeLayout();
        }
    }

    private static string DescribeRawChannel(int rawCh, RouterConfig cfg)
    {
        var mode = (cfg.MixingMode ?? "FrontBoth").Trim();
        var isMusicOnly = mode.Equals("MusicOnly", StringComparison.OrdinalIgnoreCase);
        var isShakerOnly = mode.Equals("ShakerOnly", StringComparison.OrdinalIgnoreCase);
        var isDedicated = mode.Equals("Dedicated", StringComparison.OrdinalIgnoreCase);
        var isPriority = mode.Equals("PriorityMusic", StringComparison.OrdinalIgnoreCase) || mode.Equals("PriorityShaker", StringComparison.OrdinalIgnoreCase);

        string MusicL() => $"MusicL ({FormatDb(cfg.MusicGainDb)})";
        string MusicR() => $"MusicR ({FormatDb(cfg.MusicGainDb)})";
        string ShakerL() => $"ShakerL ({FormatDb(cfg.ShakerGainDb)}, HP {cfg.ShakerHighPassHz:0.#}Hz, LP {cfg.ShakerLowPassHz:0.#}Hz)";
        string ShakerR() => $"ShakerR ({FormatDb(cfg.ShakerGainDb)}, HP {cfg.ShakerHighPassHz:0.#}Hz, LP {cfg.ShakerLowPassHz:0.#}Hz)";

        if (isPriority)
        {
            return $"{ShortName(rawCh)} = dynamic (priority switching: {mode})";
        }

        if (isMusicOnly)
        {
            return rawCh switch
            {
                0 => "FL = " + MusicL(),
                1 => "FR = " + MusicR(),
                _ => $"{ShortName(rawCh)} = 0 (MusicOnly)"
            };
        }

        // shaker is available in all other modes (including ShakerOnly)
        return rawCh switch
        {
            0 => $"FL = {(isDedicated ? MusicL() : (isShakerOnly ? ShakerL() : $"{MusicL()} + {ShakerL()}"))}",
            1 => $"FR = {(isDedicated ? MusicR() : (isShakerOnly ? ShakerR() : $"{MusicR()} + {ShakerR()}"))}",
            2 => cfg.UseCenterChannel ? $"FC = 0.5*({ShakerL()} + {ShakerR()})" : "FC = 0 (center disabled)",
            3 => $"LFE = 0.5*({ShakerL()} + {ShakerR()}) * {FormatDb(cfg.LfeGainDb)}",
            4 => $"BL = {ShakerL()} * {FormatDb(cfg.RearGainDb)}",
            5 => $"BR = {ShakerR()} * {FormatDb(cfg.RearGainDb)}",
            6 => $"SL = {ShakerL()} * {FormatDb(cfg.SideGainDb)}",
            7 => $"SR = {ShakerR()} * {FormatDb(cfg.SideGainDb)}",
            _ => $"{ShortName(rawCh)} = (unknown)"
        };
    }

    private TabPage BuildCalibrationTab()
    {
        var page = new TabPage("Calibration") { BackColor = NeonTheme.BgPrimary, ForeColor = NeonTheme.TextPrimary };

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

    private sealed record DeviceLists(List<string> RenderDevices, List<string> CaptureDevices);

    private void RefreshDeviceLists()
    {
        _ = RefreshDeviceListsAsync(showErrorDialog: true);
    }

    private async Task RefreshDeviceListsAsync(bool showErrorDialog)
    {
        if (_deviceRefreshRunning)
            return;

        _deviceRefreshRunning = true;
        try
        {
            AppLog.Info("Refreshing Windows audio device lists...");
            var task = Task.Run(EnumerateDeviceLists);
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(8))).ConfigureAwait(true);
            if (completed != task)
                throw new TimeoutException("Timed out while enumerating Windows audio devices.");

            var lists = await task.ConfigureAwait(true);
            if (IsDisposed)
                return;

            AppLog.Info($"Audio devices enumerated: render={lists.RenderDevices.Count}, capture={lists.CaptureDevices.Count}");

            BeginInvoke(new Action(() => ApplyDeviceLists(lists)));
        }
        catch (Exception ex)
        {
            _lastStartError = $"Device refresh failed: {ex.Message}";
            AppLog.Error("Device refresh failed", ex);

            if (showErrorDialog && !IsDisposed)
            {
                try
                {
                    MessageBox.Show(this, ex.Message, "Device refresh failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch
                {
                    // ignore
                }
            }
        }
        finally
        {
            _deviceRefreshRunning = false;
        }
    }

    private static DeviceLists EnumerateDeviceLists()
    {
        using var enumerator = new MMDeviceEnumerator();

        var renderDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => d.FriendlyName)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Add friendly "default" choice for Simple Mode (works anywhere a render device name is accepted).
        if (!renderDevices.Contains(DeviceHelper.DefaultSystemRenderDevice))
            renderDevices.Insert(0, DeviceHelper.DefaultSystemRenderDevice);

        var captureDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(d => d.FriendlyName)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DeviceLists(renderDevices, captureDevices);
    }

    private void ApplyDeviceLists(DeviceLists lists)
    {
        _suppressFormatUpdate = true;
        try
        {
            var devices = lists.RenderDevices;
            var captureDevices = lists.CaptureDevices;

            void SetItems(ComboBox combo, bool allowNone)
            {
                var selected = combo.SelectedItem as string;
                combo.Items.Clear();
                if (allowNone)
                    combo.Items.Add(DeviceHelper.NoneDevice);
                foreach (var name in devices) combo.Items.Add(name);
                if (!string.IsNullOrWhiteSpace(selected) && combo.Items.Contains(selected))
                    combo.SelectedItem = selected;
            }

            SetItems(_musicDeviceCombo, allowNone: false);
            SetItems(_shakerDeviceCombo, allowNone: true);
            SetItems(_outputDeviceCombo, allowNone: false);

            // Simple Mode dropdowns
            SetItems(_simpleGameSourceCombo, allowNone: false);
            SetItems(_simpleSecondarySourceCombo, allowNone: true);
            SetItems(_simpleOutputCombo, allowNone: false);

            _assistant.UpdateOutputDevices(devices.ToArray());

            // capture list
            {
                var selected = _latencyInputCombo.SelectedItem as string;
                _latencyInputCombo.Items.Clear();
                foreach (var name in captureDevices) _latencyInputCombo.Items.Add(name);
                if (!string.IsNullOrWhiteSpace(selected) && _latencyInputCombo.Items.Contains(selected))
                    _latencyInputCombo.SelectedItem = selected;
            }
        }
        finally
        {
            _suppressFormatUpdate = false;
        }
    }

    private void LoadDeviceSelectionsFromConfig()
    {
        _suppressFormatUpdate = true;
        try
        {
            SelectIfPresent(_musicDeviceCombo, _config.MusicInputRenderDevice);
            SelectIfPresent(_shakerDeviceCombo, _config.ShakerInputRenderDevice);
            SelectIfPresent(_outputDeviceCombo, _config.OutputRenderDevice);

            SelectIfPresent(_simpleGameSourceCombo, _config.MusicInputRenderDevice);

            // Secondary source may be empty; if so, leave at (None) if present.
            if (string.IsNullOrWhiteSpace(_config.ShakerInputRenderDevice))
                _simpleSecondarySourceCombo.SelectedItem = DeviceHelper.NoneDevice;
            else
                SelectIfPresent(_simpleSecondarySourceCombo, _config.ShakerInputRenderDevice);

            SelectIfPresent(_simpleOutputCombo, _config.OutputRenderDevice);
            if (!string.IsNullOrWhiteSpace(_config.LatencyInputCaptureDevice))
                SelectIfPresent(_latencyInputCombo, _config.LatencyInputCaptureDevice);
        }
        finally
        {
            _suppressFormatUpdate = false;
        }
    }

    private void LoadConfigIntoControls()
    {
        LoadDeviceSelectionsFromConfig();

        _musicGainDb.Value = (decimal)_config.MusicGainDb;
        _shakerGainDb.Value = (decimal)_config.ShakerGainDb;
        _masterGainDb.Value = (decimal)_config.MasterGainDb;

        _suppressConsoleSync = true;
        try
        {
            _simpleMasterGain.Value = (int)Math.Round(_config.MasterGainDb * 10.0);
            _simpleMasterGainLabel.Text = $"{_config.MasterGainDb:0.0} dB";

            _simpleShakerStrength.Value = (int)Math.Round(_config.LfeGainDb * 10.0);
            _simpleShakerStrengthLabel.Text = $"{_config.LfeGainDb:0.0} dB";
        }
        finally { _suppressConsoleSync = false; }

        _hpHz.Value = (decimal)_config.ShakerHighPassHz;
        _lpHz.Value = (decimal)_config.ShakerLowPassHz;

        _musicHpEnable.Checked = _config.MusicHighPassHz is not null;
        _musicHpHz.Value = (decimal)(_config.MusicHighPassHz ?? 40.0f);
        _musicLpEnable.Checked = _config.MusicLowPassHz is not null;
        _musicLpHz.Value = (decimal)(_config.MusicLowPassHz ?? 160.0f);

        _mixingModeCombo.SelectedIndex = ((_config.MixingMode ?? "FrontBoth").Trim()) switch
        {
            "MusicOnly" => 1,
            "ShakerOnly" => 2,
            "PriorityMusic" => 3,
            "PriorityShaker" => 4,
            "Dedicated" => 5,
            _ => 0
        };

        // Console sync
        _suppressConsoleSync = true;
        try
        {
            _consoleMatrixDirty = false;

            _consoleMusicGain.Value = (int)Math.Round(_config.MusicGainDb * 10.0);
            _consoleMusicGainLabel.Text = $"{_config.MusicGainDb:0.0} dB";

            _consoleShakerGain.Value = (int)Math.Round(_config.ShakerGainDb * 10.0);
            _consoleShakerGainLabel.Text = $"{_config.ShakerGainDb:0.0} dB";

            // Populate matrix: if config has no override, visualize a sensible preset from mixingMode.
            var matrix = new bool[6, 2];
            if (_config.GroupRouting is { Length: 12 } gr)
            {
                for (var r = 0; r < 6; r++)
                    for (var c = 0; c < 2; c++)
                        matrix[r, c] = gr[r * 2 + c];
            }
            else
            {
                // approximate current mixingMode
                var m = (_config.MixingMode ?? "FrontBoth").Trim();
                if (m.Equals("MusicOnly", StringComparison.OrdinalIgnoreCase))
                {
                    matrix[0, 0] = true;
                }
                else if (m.Equals("ShakerOnly", StringComparison.OrdinalIgnoreCase))
                {
                    for (var r = 0; r < 5; r++) matrix[r, 1] = true;
                }
                else if (m.Equals("Dedicated", StringComparison.OrdinalIgnoreCase))
                {
                    matrix[0, 0] = true;
                    for (var r = 1; r < 5; r++) matrix[r, 1] = true;
                }
                else
                {
                    matrix[0, 0] = true;
                    matrix[0, 1] = true;
                    for (var r = 1; r < 5; r++) matrix[r, 1] = true;
                }
            }
            _consoleMatrix.SetAll(matrix);

            // Set input mode dropdown to closest preset
            if (matrix[0, 0] && !matrix[0, 1])
                _consoleInputMode.SelectedItem = "A";
            else if (!matrix[0, 0] && matrix[0, 1])
                _consoleInputMode.SelectedItem = "B";
            else
                _consoleInputMode.SelectedItem = "A+B";

            for (var ch = 0; ch < 8; ch++)
            {
                _consoleOutGain[ch].Value = _channelSliders[ch].Value;
                _consoleOutGainLabel[ch].Text = _channelLabels[ch].Text;
                _consoleOutMute[ch].Checked = _channelMute[ch].Checked;
                _consoleOutSolo[ch].Checked = _channelSolo[ch].Checked;
            }
        }
        finally
        {
            _suppressConsoleSync = false;
        }

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
        UpdateRoutingGrid();
        UpdateStatusBar();
    }

    private void SaveConfigFromControls(bool showSavedDialog = true)
    {
        _config.MusicInputRenderDevice = _musicDeviceCombo.SelectedItem as string ?? _config.MusicInputRenderDevice;
        {
            var secondary = _shakerDeviceCombo.SelectedItem as string;
            _config.ShakerInputRenderDevice = string.Equals(secondary, DeviceHelper.NoneDevice, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : (secondary ?? _config.ShakerInputRenderDevice);
        }
        _config.OutputRenderDevice = _outputDeviceCombo.SelectedItem as string ?? _config.OutputRenderDevice;
        _config.LatencyInputCaptureDevice = _latencyInputCombo.SelectedItem as string ?? _config.LatencyInputCaptureDevice;

        _config.MusicGainDb = (float)_musicGainDb.Value;
        _config.ShakerGainDb = (float)_shakerGainDb.Value;
        _config.MasterGainDb = (float)_masterGainDb.Value;

        // Simple Mode "Bass shaker strength" is LFE gain in dB.
        _config.LfeGainDb = _simpleShakerStrength.Value / 10.0f;

        _config.MusicHighPassHz = _musicHpEnable.Checked ? (float)_musicHpHz.Value : null;
        _config.MusicLowPassHz = _musicLpEnable.Checked ? (float)_musicLpHz.Value : null;

        _config.ShakerHighPassHz = (float)_hpHz.Value;
        _config.ShakerLowPassHz = (float)_lpHz.Value;

        _config.MixingMode = _mixingModeCombo.SelectedIndex switch
        {
            1 => "MusicOnly",
            2 => "ShakerOnly",
            3 => "PriorityMusic",
            4 => "PriorityShaker",
            5 => "Dedicated",
            _ => "FrontBoth"
        };

        // Routing matrix override: only persist if user edited it (or if it was already present).
        if (_consoleMatrixDirty || _config.GroupRouting is not null)
            _config.GroupRouting = ReadMatrixToConfig();
        else
            _config.GroupRouting = null;

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
        UpdateRoutingGrid();
        UpdateStatusBar();
    }

    private void RefreshStatusPresetMenu()
    {
        _statusPreset.DropDownItems.Clear();

        foreach (var p in ProfileStore.LoadAll())
        {
            var name = p.Name;
            var item = new ToolStripMenuItem(name);
            item.Click += (_, _) =>
            {
                _profileCombo.SelectedItem = name;
                LoadSelectedProfileIntoUi();
                UpdateStatusBar();
            };
            _statusPreset.DropDownItems.Add(item);
        }
    }

    private static string DescribeInputsForRawChannel(int rawCh, RouterConfig cfg)
    {
        var mode = (cfg.MixingMode ?? "FrontBoth").Trim();
        if (mode.Equals("MusicOnly", StringComparison.OrdinalIgnoreCase))
            return rawCh is 0 or 1 ? "Music" : "-";
        if (mode.Equals("ShakerOnly", StringComparison.OrdinalIgnoreCase))
            return "Shaker";
        if (mode.Equals("PriorityMusic", StringComparison.OrdinalIgnoreCase) || mode.Equals("PriorityShaker", StringComparison.OrdinalIgnoreCase))
            return $"Priority ({(mode.Equals("PriorityMusic", StringComparison.OrdinalIgnoreCase) ? "Music" : "Shaker")})";

        if (mode.Equals("Dedicated", StringComparison.OrdinalIgnoreCase))
            return rawCh is 0 or 1 ? "Music" : "Shaker";

        // FrontBoth
        return rawCh is 0 or 1 ? "Music + Shaker" : "Shaker";
    }

    private void UpdateStatusBar()
    {
        var running = _router is not null;
        var outDev = _outputDeviceCombo.SelectedItem as string;
        var mode = _useExclusiveMode.Checked ? "Exclusive" : "Shared";
        var sr = running ? _router!.EffectiveSampleRate : GetSelectedSampleRate();
        var preset = _profileCombo.SelectedItem as string;

        var gamePeak = Math.Max(_displayMusic[0], _displayMusic[1]);
        var secondaryPeak = Math.Max(_displayShaker[0], _displayShaker[1]);
        var gameDetected = running && gamePeak > 0.0015f;
        var secondaryDetected = running && secondaryPeak > 0.0015f;

        var outputSelected = !string.IsNullOrWhiteSpace(outDev);
        var outputOk = true;
        if (outputSelected)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastOutputCheckUtc).TotalMilliseconds > 1000)
            {
                _lastOutputCheckUtc = now;
                try
                {
                    // Best-effort: validate selection still exists.
                    using var _ = DeviceHelper.GetRenderDeviceByFriendlyName(outDev!);
                    _cachedOutputOk = true;
                }
                catch
                {
                    _cachedOutputOk = false;
                }
            }
            outputOk = _cachedOutputOk;
        }

        var speakersEnabled = true;
        var shakerEnabled = true;
        if (_config.GroupRouting is { Length: 12 } gr)
        {
            // Front row (0) and LFE row (2)
            speakersEnabled = gr[0] || gr[1];
            shakerEnabled = gr[2 * 2] || gr[2 * 2 + 1];
        }

        var cOk = ColorTranslator.FromHtml("#00E676");
        var cWarn = ColorTranslator.FromHtml("#FFB000");
        var cErr = ColorTranslator.FromHtml("#FF3B3B");

        string healthText;
        Color healthColor;

        if (!outputSelected)
        {
            healthText = "Error: select an output device";
            healthColor = cErr;
        }
        else if (!outputOk)
        {
            healthText = "Error: output device disconnected";
            healthColor = cErr;
        }
        else if (!string.IsNullOrWhiteSpace(_lastStartError) && !running)
        {
            healthText = $"Error: {_lastStartError}";
            healthColor = cErr;
        }
        else if (!running)
        {
            healthText = "Ready: select devices, pick a preset, press Start";
            healthColor = NeonTheme.TextMuted;
        }
        else if (!gameDetected)
        {
            healthText = "Warning: routing active – no audio from Game Source";
            healthColor = cWarn;
        }
        else
        {
            healthText = "OK: routing active – audio detected";
            healthColor = cOk;
        }

        _statusHealth.Text = healthText;
        _statusHealth.ForeColor = healthColor;

        _simpleStatus.Text = healthText;
        _simpleStatus.ForeColor = healthColor;

        _simpleFlow.RouterRunning = running;
        _simpleFlow.OutputOk = outputSelected && outputOk;
        _simpleFlow.GameAudioDetected = gameDetected;
        _simpleFlow.SecondaryAudioDetected = secondaryDetected;
        _simpleFlow.SpeakersEnabled = speakersEnabled;
        _simpleFlow.ShakerEnabled = shakerEnabled;
        _simpleFlow.OutputDeviceDisplay = string.IsNullOrWhiteSpace(outDev) ? "Output" : outDev!;
        _simpleFlow.Invalidate();

        _statusRouter.Text = running ? "Router: Running" : "Router: Stopped";
        _statusFormat.Text = $"Output: {(string.IsNullOrWhiteSpace(outDev) ? "(not selected)" : outDev)} | {sr} Hz | {mode}";
        _statusLatency.Text = $"Latency: {(int)_latencyMs.Value} ms | Mix: {_mixingModeCombo.SelectedItem}";
        _statusPreset.Text = string.IsNullOrWhiteSpace(preset) ? "Preset" : $"Preset: {preset}";

        _assistant.UpdateSnapshot(BuildCopilotContext());
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
            _lastStartError = null;
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

            _simpleStartButton.Enabled = false;
            _simpleStopButton.Enabled = true;

            UpdateDiagnostics();
            UpdateStatusBar();
        }
        catch (Exception ex)
        {
            _lastStartError = ex.Message;
            StopRouter();
            MessageBox.Show(this, ex.Message, "Failed to start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateStatusBar();
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

            _simpleStartButton.Enabled = true;
            _simpleStopButton.Enabled = false;

            UpdateDiagnostics();
            UpdateStatusBar();
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
