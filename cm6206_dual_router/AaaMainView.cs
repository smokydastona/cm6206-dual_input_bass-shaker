using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal sealed class AaaMainView : UserControl
{
    private readonly TableLayoutPanel _root;
    private readonly TableLayoutPanel _content;

    private readonly RoundedPanel _header;
    private readonly RoundedPanel _left;
    private readonly RoundedPanel _center;
    private readonly RoundedPanel _right;
    private readonly RoundedPanel _status;

    // Header content
    private readonly PictureBox _appIcon;
    private readonly Label _title;
    public readonly StatusPill DevicePill;
    public readonly Label SampleRateLabel;
    public readonly Label LatencyLabel;
    public readonly Button SettingsButton;

    // Left sidebar content
    public readonly Button PresetGaming;
    public readonly Button PresetMovies;
    public readonly Button PresetMusic;
    public readonly Button PresetCustom;

    public readonly ComboBox OutputDevice;
    public readonly ComboBox InputA;
    public readonly ComboBox InputB;

    // Center panel regions
    private readonly RoundedPanel _signalChain;
    private readonly RoundedPanel _inputControls;
    private readonly RoundedPanel _dsp;

    // Input controls A/B
    public readonly NeonMeter InputAMeter;
    public readonly NeonMeter InputBMeter;
    public readonly NeonKnob InputAGain;
    public readonly NeonKnob InputBGain;
    public readonly NeonToggleButton InputAMute;
    public readonly NeonToggleButton InputASolo;
    public readonly NeonToggleButton InputAPhase;
    public readonly NeonToggleButton InputBMute;
    public readonly NeonToggleButton InputBSolo;
    public readonly NeonToggleButton InputBPhase;

    // DSP controls
    public readonly NeonToggleButton LpfEnable;
    public readonly NeonSlider LpfFreq;
    public readonly ComboBox LpfSlope;

    public readonly NeonToggleButton HpfEnable;
    public readonly NeonSlider HpfFreq;
    public readonly ComboBox HpfSlope;

    public readonly NeonToggleButton LimiterEnable;
    public readonly NeonKnob LimiterThreshold;
    public readonly ProgressBar LimiterGr;

    public readonly NeonKnob OutputGain;

    // Right sidebar
    public readonly NeonMeter OutputMeter;
    public readonly StatusPill LimiterPill;
    public readonly NeonMeter InputAMeterMini;
    public readonly NeonMeter InputBMeterMini;
    public readonly Button OpenLogFolder;

    // Status bar
    public readonly Label StatusText;
    public readonly Label BuildInfo;

    public AaaMainView()
    {
        Dock = DockStyle.Fill;
        BackColor = NeonTheme.BgPrimary;
        ForeColor = NeonTheme.TextPrimary;

        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = NeonTheme.BgPrimary
        };

        _header = MakeRegionPanel();
        _left = MakeRegionPanel();
        _center = MakeRegionPanel();
        _right = MakeRegionPanel();
        _status = MakeRegionPanel();

        _content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = NeonTheme.BgPrimary
        };

        // Header layout
        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = NeonTheme.BgPanel,
            Padding = new Padding(12, 6, 12, 6)
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));

        _appIcon = new PictureBox { SizeMode = PictureBoxSizeMode.CenterImage, Dock = DockStyle.Fill };
        _title = new Label { Text = "CM6206 Dual-Input Bass Shaker", AutoSize = true, ForeColor = NeonTheme.TextPrimary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

        DevicePill = new StatusPill { Dock = DockStyle.Fill, Text = "Device: (unknown)" };
        SampleRateLabel = new Label { Text = "Sample rate: (unknown)", AutoSize = true, ForeColor = NeonTheme.TextSecondary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        LatencyLabel = new Label { Text = "Latency: (unknown)", AutoSize = true, ForeColor = NeonTheme.TextSecondary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        SettingsButton = new Button { Text = "⚙", Dock = DockStyle.Fill };

        headerLayout.Controls.Add(_appIcon, 0, 0);
        headerLayout.Controls.Add(_title, 1, 0);
        headerLayout.Controls.Add(DevicePill, 2, 0);
        headerLayout.Controls.Add(SampleRateLabel, 3, 0);
        headerLayout.Controls.Add(LatencyLabel, 4, 0);
        headerLayout.Controls.Add(SettingsButton, 5, 0);
        _header.Controls.Add(headerLayout);

        // Left sidebar
        var leftLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(16)
        };

        leftLayout.Controls.Add(new Label { Text = "Presets", AutoSize = true, ForeColor = NeonTheme.TextPrimary });
        PresetGaming = MakeSidebarButton("Gaming");
        PresetMovies = MakeSidebarButton("Movies");
        PresetMusic = MakeSidebarButton("Music");
        PresetCustom = MakeSidebarButton("Custom");
        leftLayout.Controls.Add(PresetGaming);
        leftLayout.Controls.Add(PresetMovies);
        leftLayout.Controls.Add(PresetMusic);
        leftLayout.Controls.Add(PresetCustom);

        var divider = new Panel { Height = 1, Width = 220, BackColor = Color.FromArgb(60, 255, 255, 255), Margin = new Padding(0, 12, 0, 12) };
        leftLayout.Controls.Add(divider);

        leftLayout.Controls.Add(new Label { Text = "Output device", AutoSize = true, ForeColor = NeonTheme.TextSecondary });
        OutputDevice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        leftLayout.Controls.Add(OutputDevice);

        leftLayout.Controls.Add(new Label { Text = "Input A", AutoSize = true, ForeColor = NeonTheme.TextSecondary });
        InputA = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        leftLayout.Controls.Add(InputA);

        leftLayout.Controls.Add(new Label { Text = "Input B", AutoSize = true, ForeColor = NeonTheme.TextSecondary });
        InputB = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        leftLayout.Controls.Add(InputB);

        _left.Controls.Add(leftLayout);

        // Center panel regions
        _center.Padding = new Padding(16);
        var centerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = NeonTheme.BgPrimary
        };

        _signalChain = MakeCard();
        _inputControls = MakeCard();
        _dsp = MakeCard();

        centerLayout.Controls.Add(_signalChain, 0, 0);
        centerLayout.Controls.Add(_inputControls, 0, 1);
        centerLayout.Controls.Add(_dsp, 0, 2);

        centerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 288));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _center.Controls.Add(centerLayout);

        BuildSignalChainUi(_signalChain);

        // Input controls panel
        var inputs = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = NeonTheme.BgPanel,
            Padding = new Padding(16)
        };
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var inputACol = MakeCard();
        var inputBCol = MakeCard();
        inputACol.BackColor = NeonTheme.BgRaised;
        inputBCol.BackColor = NeonTheme.BgRaised;

        inputs.Controls.Add(inputACol, 0, 0);
        inputs.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent }, 1, 0);
        inputs.Controls.Add(inputBCol, 2, 0);

        _inputControls.Controls.Add(inputs);

        InputAMeter = new NeonMeter { Vertical = true, Width = 16, Height = 160 };
        InputBMeter = new NeonMeter { Vertical = true, Width = 16, Height = 160 };
        InputAGain = new NeonKnob { Width = 64, Height = 64 };
        InputBGain = new NeonKnob { Width = 64, Height = 64 };
        InputAMute = new NeonToggleButton { Text = "Mute", Width = 56, Height = 28 };
        InputASolo = new NeonToggleButton { Text = "Solo", Width = 56, Height = 28 };
        InputAPhase = new NeonToggleButton { Text = "Phase", Width = 56, Height = 28 };
        InputBMute = new NeonToggleButton { Text = "Mute", Width = 56, Height = 28 };
        InputBSolo = new NeonToggleButton { Text = "Solo", Width = 56, Height = 28 };
        InputBPhase = new NeonToggleButton { Text = "Phase", Width = 56, Height = 28 };

        BuildInputColumn(inputACol, "Input A", InputAMeter, InputAGain, InputAMute, InputASolo, InputAPhase);
        BuildInputColumn(inputBCol, "Input B", InputBMeter, InputBGain, InputBMute, InputBSolo, InputBPhase);

        // DSP chain panel
        var dspGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = NeonTheme.BgPanel,
            Padding = new Padding(0)
        };
        for (var i = 0; i < 4; i++)
            dspGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        var lpf = MakeDspSection("LPF");
        var hpf = MakeDspSection("HPF");
        var limiter = MakeDspSection("Limiter");
        var output = MakeDspSection("Output");

        dspGrid.Controls.Add(lpf, 0, 0);
        dspGrid.Controls.Add(hpf, 1, 0);
        dspGrid.Controls.Add(limiter, 2, 0);
        dspGrid.Controls.Add(output, 3, 0);

        _dsp.Controls.Add(dspGrid);

        // LPF
        LpfEnable = new NeonToggleButton { Text = "On", Width = 56, Height = 28 };
        LpfFreq = new NeonSlider { Width = 260, Height = 24 };
        LpfSlope = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        LpfSlope.Items.AddRange(["12 dB/oct", "24 dB/oct", "48 dB/oct"]);
        BuildFilterSection(lpf, LpfEnable, LpfFreq, LpfSlope);

        // HPF
        HpfEnable = new NeonToggleButton { Text = "On", Width = 56, Height = 28 };
        HpfFreq = new NeonSlider { Width = 260, Height = 24 };
        HpfSlope = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        HpfSlope.Items.AddRange(["12 dB/oct", "24 dB/oct", "48 dB/oct"]);
        BuildFilterSection(hpf, HpfEnable, HpfFreq, HpfSlope);

        // Limiter
        LimiterEnable = new NeonToggleButton { Text = "On", Width = 56, Height = 28 };
        LimiterThreshold = new NeonKnob { Width = 48, Height = 48 };
        LimiterGr = new ProgressBar { Width = 80, Height = 12, Style = ProgressBarStyle.Continuous };
        BuildLimiterSection(limiter);

        // Output
        OutputGain = new NeonKnob { Width = 64, Height = 64 };
        BuildOutputSection(output);

        // Right sidebar
        var rightLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(16)
        };

        rightLayout.Controls.Add(new Label { Text = "Output Monitoring", AutoSize = true, ForeColor = NeonTheme.TextPrimary });
        OutputMeter = new NeonMeter { Vertical = true, Width = 24, Height = 220 };
        rightLayout.Controls.Add(OutputMeter);

        LimiterPill = new StatusPill { Width = 200, Height = 28, Text = "Limiter: (unknown)" };
        rightLayout.Controls.Add(LimiterPill);

        rightLayout.Controls.Add(new Label { Text = "Input A", AutoSize = true, ForeColor = NeonTheme.TextSecondary });
        InputAMeterMini = new NeonMeter { Vertical = true, Width = 12, Height = 120 };
        rightLayout.Controls.Add(InputAMeterMini);

        rightLayout.Controls.Add(new Label { Text = "Input B", AutoSize = true, ForeColor = NeonTheme.TextSecondary });
        InputBMeterMini = new NeonMeter { Vertical = true, Width = 12, Height = 120 };
        rightLayout.Controls.Add(InputBMeterMini);

        var safety = MakeCard();
        safety.Height = 100;
        safety.Controls.Add(new Label { Text = "Safety indicators", AutoSize = true, ForeColor = NeonTheme.TextSecondary, Dock = DockStyle.Top });
        rightLayout.Controls.Add(safety);

        OpenLogFolder = new Button { Text = "Open Log Folder", Width = 200, Height = 36 };
        rightLayout.Controls.Add(OpenLogFolder);

        _right.Controls.Add(rightLayout);

        // Status bar
        var statusLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = NeonTheme.BgPanel,
            Padding = new Padding(12, 6, 12, 6)
        };
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));

        StatusText = new Label { Text = "Status: (starting)", AutoSize = true, ForeColor = NeonTheme.TextSecondary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        BuildInfo = new Label { Text = "Build: (unknown)", AutoSize = true, ForeColor = NeonTheme.TextSecondary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };

        statusLayout.Controls.Add(StatusText, 0, 0);
        statusLayout.Controls.Add(BuildInfo, 1, 0);
        _status.Controls.Add(statusLayout);

        // Compose content
        _content.Controls.Add(_left, 0, 0);
        _content.Controls.Add(_center, 1, 0);
        _content.Controls.Add(_right, 2, 0);

        _root.Controls.Add(_header, 0, 0);
        _root.Controls.Add(_content, 0, 1);
        _root.Controls.Add(_status, 0, 2);

        Controls.Add(_root);

        // Defaults
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            _appIcon.Image = icon?.ToBitmap();
        }
        catch
        {
            // ignore
        }

        ApplyScaledLayout();
    }

    public void ApplyScaledLayout()
    {
        var scale = AaaUiMetrics.ComputeScale(Parent?.ClientSize ?? ClientSize);

        var margin = AaaUiMetrics.S(scale, AaaUiMetrics.OuterMargin);
        var headerH = AaaUiMetrics.S(scale, AaaUiMetrics.HeaderHeight);
        var statusH = AaaUiMetrics.S(scale, AaaUiMetrics.StatusHeight);
        var leftW = AaaUiMetrics.S(scale, AaaUiMetrics.LeftSidebarWidth);
        var rightW = AaaUiMetrics.S(scale, AaaUiMetrics.RightSidebarWidth);

        Padding = new Padding(margin);

        _root.RowStyles.Clear();
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, headerH));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, statusH));

        _content.ColumnStyles.Clear();
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, leftW));
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, rightW));

        // DSP card sizing hint
        var dspH = AaaUiMetrics.S(scale, AaaUiMetrics.DspSectionHeight);
        _dsp.MinimumSize = new Size(0, dspH);
    }

    private static RoundedPanel MakeRegionPanel()
    {
        return new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = NeonTheme.BgPanel,
            CornerRadius = 8,
            BorderColor = Color.FromArgb(40, 255, 255, 255)
        };
    }

    private static RoundedPanel MakeCard()
    {
        return new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = NeonTheme.BgPanel,
            CornerRadius = 8,
            BorderColor = Color.FromArgb(50, 255, 255, 255)
        };
    }

    private static Button MakeSidebarButton(string text)
    {
        return new Button
        {
            Text = text,
            Width = 220,
            Height = 40
        };
    }

    private static RoundedPanel MakeDspSection(string title)
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = NeonTheme.BgRaised,
            CornerRadius = 8,
            BorderColor = Color.FromArgb(60, 255, 255, 255),
            Padding = new Padding(16),
            Margin = new Padding(8)
        };

        panel.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = NeonTheme.TextPrimary, Dock = DockStyle.Top });
        return panel;
    }

    private static void BuildSignalChainUi(Control host)
    {
        host.BackColor = NeonTheme.BgPanel;
        host.Padding = new Padding(16);

        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true
        };

        void AddNode(string text)
        {
            var node = new RoundedPanel
            {
                Width = 140,
                Height = 60,
                BackColor = NeonTheme.BgRaised,
                CornerRadius = 8,
                BorderColor = Color.FromArgb(60, 255, 255, 255),
                Margin = new Padding(0, 0, 10, 0)
            };
            node.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = NeonTheme.TextPrimary });
            row.Controls.Add(node);
        }

        void AddArrow()
        {
            row.Controls.Add(new Label { Text = "→", AutoSize = true, ForeColor = NeonTheme.TextSecondary, Padding = new Padding(6, 16, 6, 0) });
        }

        AddNode("Input A");
        AddArrow();
        AddNode("Input B");
        AddArrow();
        AddNode("Mix");
        AddArrow();
        AddNode("Filters");
        AddArrow();
        AddNode("Limiter");
        AddArrow();
        AddNode("Output");

        host.Controls.Add(row);
    }

    private static void BuildInputColumn(Control col, string title, NeonMeter meter, NeonKnob gain, NeonToggleButton mute, NeonToggleButton solo, NeonToggleButton phase)
    {
        col.Padding = new Padding(16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var header = new Label { Text = title, AutoSize = true, ForeColor = NeonTheme.TextPrimary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        layout.Controls.Add(header, 0, 0);
        layout.SetColumnSpan(header, 4);

        layout.Controls.Add(meter, 0, 1);
        layout.Controls.Add(gain, 1, 1);

        var toggles = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        toggles.Controls.Add(mute);
        toggles.Controls.Add(solo);
        toggles.Controls.Add(phase);

        layout.Controls.Add(toggles, 1, 2);
        layout.SetColumnSpan(toggles, 3);

        col.Controls.Add(layout);
    }

    private static void BuildFilterSection(Control host, NeonToggleButton enable, NeonSlider freq, ComboBox slope)
    {
        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 28, 0, 0)
        };

        body.Controls.Add(new Label { Text = "Enable", AutoSize = true, ForeColor = NeonTheme.TextSecondary });
        body.Controls.Add(enable);

        body.Controls.Add(new Label { Text = "Frequency", AutoSize = true, ForeColor = NeonTheme.TextSecondary, Margin = new Padding(0, 10, 0, 0) });
        body.Controls.Add(freq);

        body.Controls.Add(new Label { Text = "Slope", AutoSize = true, ForeColor = NeonTheme.TextSecondary, Margin = new Padding(0, 10, 0, 0) });
        body.Controls.Add(slope);

        host.Controls.Add(body);
    }

    private void BuildLimiterSection(Control host)
    {
        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 28, 0, 0)
        };

        body.Controls.Add(new Label { Text = "Enable", AutoSize = true, ForeColor = NeonTheme.TextSecondary });
        body.Controls.Add(LimiterEnable);

        body.Controls.Add(new Label { Text = "Threshold", AutoSize = true, ForeColor = NeonTheme.TextSecondary, Margin = new Padding(0, 10, 0, 0) });
        body.Controls.Add(LimiterThreshold);

        body.Controls.Add(new Label { Text = "GR", AutoSize = true, ForeColor = NeonTheme.TextSecondary, Margin = new Padding(0, 10, 0, 0) });
        body.Controls.Add(LimiterGr);

        host.Controls.Add(body);
    }

    private void BuildOutputSection(Control host)
    {
        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 28, 0, 0)
        };

        body.Controls.Add(new Label { Text = "Output gain", AutoSize = true, ForeColor = NeonTheme.TextSecondary });
        body.Controls.Add(OutputGain);

        host.Controls.Add(body);
    }
}
