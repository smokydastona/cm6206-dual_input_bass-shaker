using System.Text;
using System.Windows.Forms;

namespace Cm6206DualRouter;

internal sealed class SetupAssistantPanel : UserControl
{
    private readonly Label _title = new()
    {
        Text = "Setup Assistant",
        AutoSize = true,
        Font = NeonTheme.CreateBaseFont(12, FontStyle.Bold),
        ForeColor = NeonTheme.TextPrimary
    };

    private readonly Label _subtitle = new()
    {
        Text = "Guided help. Executes changes only with permission.",
        AutoSize = true,
        ForeColor = NeonTheme.TextMuted
    };

    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        WordWrap = true
    };

    private readonly FlowLayoutPanel _actions = new()
    {
        Dock = DockStyle.Bottom,
        AutoSize = true,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        Padding = new Padding(6)
    };

    private readonly Panel _hintBar = new() { Dock = DockStyle.Top, Height = 38, Visible = false };
    private readonly Label _hintText = new() { AutoSize = true, ForeColor = NeonTheme.TextPrimary, Padding = new Padding(8, 9, 0, 0) };
    private readonly Button _hintHelp = new() { Text = "Help", Width = 80, Height = 26 };

    private readonly GroupBox _settingsGroup = new() { Text = "AI (Experimental)", Dock = DockStyle.Bottom, Height = 195 };
    private readonly CheckBox _enableAi = new() { Text = "Enable AI Copilot", AutoSize = true };
    private readonly CheckBox _enableProactiveHints = new() { Text = "Show proactive hints (status monitoring)", AutoSize = true };
    private readonly TextBox _apiKey = new() { Width = 230, UseSystemPasswordChar = true };
    private readonly TextBox _model = new() { Width = 230 };
    private readonly Button _saveSettings = new() { Text = "Save", Width = 80 };
    private readonly Label _costNote = new()
    {
        AutoSize = true,
        ForeColor = NeonTheme.TextMuted,
        Text = "Uses small text requests only. Typical usage: pennies/month."
    };

    private AiSettings _settings = AiSettings.Default;

    private readonly Panel _commandPanel = new() { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(6) };
    private readonly TextBox _commandBox = new() { Width = 210 };
    private readonly Button _sendCommand = new() { Text = "Send", Width = 70 };
    private readonly Button _explain = new() { Text = "Explain", Width = 70 };

    private readonly FlowLayoutPanel _aiSuggestionRow = new()
    {
        Dock = DockStyle.Bottom,
        AutoSize = true,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        Padding = new Padding(6)
    };

    private CopilotResponse? _lastAiResponse;

    public Func<string, Task<CopilotResponse>>? RunAiCommandAsync { get; set; }
    public Func<Task<CopilotResponse>>? ExplainAsync { get; set; }

    private enum WizardStage { None, OfferHelp, Purpose, OutputDevice, ShakerMode, Confirm }
    private WizardStage _stage = WizardStage.None;
    private string? _purpose;
    private string? _chosenOutput;
    private string? _shakerMode;

    private string[] _outputDevices = Array.Empty<string>();

    private DateTime _lastGameAudioUtc = DateTime.MinValue;
    private DateTime _lastLfeAudioUtc = DateTime.MinValue;
    private string? _activeHintKey;

    public Func<CopilotContext>? GetContext { get; set; }
    public Action<CopilotAction[]>? ApplyActionsWithConfirmation { get; set; }
    public Action<AiSettings>? SaveSettings { get; set; }

    public SetupAssistantPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = NeonTheme.BgPrimary;
        ForeColor = NeonTheme.TextPrimary;

        _hintBar.BackColor = NeonTheme.BgPanel;
        _hintHelp.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _hintHelp.Left = 0;
        _hintHelp.Top = 6;
        _hintHelp.Click += (_, _) => OnHintHelpClicked();

        _hintBar.Controls.Add(_hintText);
        _hintBar.Controls.Add(_hintHelp);
        _hintBar.Resize += (_, _) =>
        {
            _hintHelp.Left = _hintBar.Width - _hintHelp.Width - 10;
            _hintHelp.Top = 6;
        };

        var header = new Panel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(10, 10, 10, 6) };
        header.Controls.Add(_title);
        _subtitle.Top = 28;
        header.Controls.Add(_subtitle);

        Controls.Add(_log);
        Controls.Add(_actions);
        Controls.Add(_aiSuggestionRow);
        Controls.Add(_commandPanel);
        Controls.Add(_settingsGroup);
        Controls.Add(_hintBar);
        Controls.Add(header);

        BuildSettingsUi();
        BuildCommandUi();
        ApplyNeonStyle();

        Append("Idle. Click 'Help me set this up' to get started.");
        RebuildActions();
    }

    private void BuildSettingsUi()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(10),
            AutoSize = true
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        grid.Controls.Add(_enableAi, 0, 0);
        grid.SetColumnSpan(_enableAi, 2);

        grid.Controls.Add(_enableProactiveHints, 0, 1);
        grid.SetColumnSpan(_enableProactiveHints, 2);

        grid.Controls.Add(new Label { Text = "OpenAI API Key", AutoSize = true, Padding = new Padding(0, 6, 8, 0) }, 0, 2);
        grid.Controls.Add(_apiKey, 1, 2);

        grid.Controls.Add(new Label { Text = "Model", AutoSize = true, Padding = new Padding(0, 6, 8, 0) }, 0, 3);
        grid.Controls.Add(_model, 1, 3);

        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        row.Controls.Add(_saveSettings);
        row.Controls.Add(_costNote);
        grid.Controls.Add(row, 0, 4);
        grid.SetColumnSpan(row, 2);

        _enableAi.CheckedChanged += (_, _) =>
        {
            _settings = _settings with { Enabled = _enableAi.Checked };
            SaveSettings?.Invoke(_settings);
            Append(_settings.Enabled ? "AI Copilot enabled." : "AI Copilot disabled.");
            if (!_settings.Enabled)
                HideHint();
            StartFirstRunIfNeeded();
        };

        _enableProactiveHints.CheckedChanged += (_, _) =>
        {
            _settings = _settings with { ProactiveHintsEnabled = _enableProactiveHints.Checked };
            SaveSettings?.Invoke(_settings);
            if (!_settings.ProactiveHintsEnabled)
                HideHint();
            Append(_settings.ProactiveHintsEnabled ? "Proactive hints enabled." : "Proactive hints disabled.");
        };

        _saveSettings.Click += (_, _) =>
        {
            var enc = AiSettingsStore.ProtectApiKey(_apiKey.Text);
            var model = string.IsNullOrWhiteSpace(_model.Text) ? AiSettings.Default.Model : _model.Text.Trim();
            _settings = _settings with { EncryptedApiKey = enc, Model = model };
            SaveSettings?.Invoke(_settings);
            Append("AI settings saved.");
        };

        _settingsGroup.Controls.Add(grid);
    }

    private void ApplyNeonStyle()
    {
        _log.BackColor = NeonTheme.BgRaised;
        _log.ForeColor = NeonTheme.TextPrimary;
        _log.BorderStyle = BorderStyle.FixedSingle;

        _commandBox.BackColor = NeonTheme.BgRaised;
        _commandBox.ForeColor = NeonTheme.TextPrimary;
        _commandBox.BorderStyle = BorderStyle.FixedSingle;
    }

    private void BuildCommandUi()
    {
        _commandBox.PlaceholderText = "Type a setup command (optional)";

        _sendCommand.Click += async (_, _) =>
        {
            var text = _commandBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;
            if (RunAiCommandAsync is null)
            {
                Append("AI command handler is not available.");
                return;
            }

            _sendCommand.Enabled = false;
            _explain.Enabled = false;
            try
            {
                Append($"> {text}");
                var resp = await RunAiCommandAsync(text);
                RenderAiResponse(resp);
            }
            catch (Exception ex)
            {
                Append(ex.Message);
            }
            finally
            {
                _sendCommand.Enabled = true;
                _explain.Enabled = true;
            }
        };

        _explain.Click += async (_, _) =>
        {
            if (ExplainAsync is null)
            {
                Append("AI explain handler is not available.");
                return;
            }

            _sendCommand.Enabled = false;
            _explain.Enabled = false;
            try
            {
                var resp = await ExplainAsync();
                RenderAiResponse(resp);
            }
            catch (Exception ex)
            {
                Append(ex.Message);
            }
            finally
            {
                _sendCommand.Enabled = true;
                _explain.Enabled = true;
            }
        };

        _commandPanel.Controls.Add(_commandBox);
        _commandPanel.Controls.Add(_sendCommand);
        _commandPanel.Controls.Add(_explain);
        _commandPanel.Resize += (_, _) =>
        {
            _commandBox.Left = 6;
            _commandBox.Top = 10;
            _commandBox.Width = Math.Max(120, _commandPanel.Width - 6 - 70 - 70 - 20);

            _sendCommand.Left = _commandBox.Right + 6;
            _sendCommand.Top = 8;
            _explain.Left = _sendCommand.Right + 6;
            _explain.Top = 8;
        };
    }

    private void RenderAiResponse(CopilotResponse resp)
    {
        _lastAiResponse = resp;
        if (!string.IsNullOrWhiteSpace(resp.AssistantText))
            Append(resp.AssistantText);

        _aiSuggestionRow.Controls.Clear();

        if (resp.Clarification is not null)
        {
            Append(resp.Clarification.Question);
            foreach (var opt in resp.Clarification.Options)
            {
                var b = new Button { Text = opt, Height = 28, AutoSize = true };
                b.Click += async (_, _) =>
                {
                    if (RunAiCommandAsync is null) return;
                    var followUp = $"My choice: {opt}";
                    var follow = await RunAiCommandAsync(followUp);
                    RenderAiResponse(follow);
                };
                _aiSuggestionRow.Controls.Add(b);
            }
            return;
        }

        if (resp.ProposedActions.Length > 0)
        {
            var show = new Button { Text = "Show plan", Width = 90, Height = 28 };
            var apply = new Button { Text = "Apply", Width = 80, Height = 28 };

            show.Click += (_, _) =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Proposed actions:");
                foreach (var a in resp.ProposedActions)
                    sb.AppendLine($"- {a.Type} {a.StringValue ?? a.FloatValue?.ToString() ?? a.IntValue?.ToString() ?? a.BoolValue?.ToString()}");
                Append(sb.ToString().TrimEnd());
            };

            apply.Click += (_, _) => ApplyActionsWithConfirmation?.Invoke(resp.ProposedActions);

            _aiSuggestionRow.Controls.Add(show);
            _aiSuggestionRow.Controls.Add(apply);
        }
    }

    public void LoadSettings(AiSettings settings)
    {
        _settings = settings;
        _enableAi.Checked = settings.Enabled;
        _enableProactiveHints.Checked = settings.ProactiveHintsEnabled;
        _model.Text = settings.Model;
        _apiKey.Text = "";
        _apiKey.PlaceholderText = string.IsNullOrWhiteSpace(settings.EncryptedApiKey)
            ? ""
            : "Saved (enter to replace)";
        StartFirstRunIfNeeded();
    }

    public void UpdateOutputDevices(string[] outputDevices)
    {
        _outputDevices = outputDevices.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
    }

    public void UpdateSnapshot(CopilotContext ctx)
    {
        // Keep this non-intrusive: only show a hint when something is clearly wrong.
        if (!_settings.ProactiveHintsEnabled)
        {
            HideHint();
            return;
        }

        var now = DateTime.UtcNow;
        if (!ctx.RouterRunning)
        {
            _lastGameAudioUtc = now;
            _lastLfeAudioUtc = now;
        }

        if (ctx.RouterRunning && _lastGameAudioUtc == DateTime.MinValue)
            _lastGameAudioUtc = now;
        if (ctx.RouterRunning && _lastLfeAudioUtc == DateTime.MinValue)
            _lastLfeAudioUtc = now;

        var gameDetected = ctx.RouterRunning && ctx.GamePeak > 0.0015f;
        var lfeDetected = ctx.RouterRunning && ctx.OutputLfePeak > 0.0015f;

        if (gameDetected)
            _lastGameAudioUtc = now;
        if (lfeDetected)
            _lastLfeAudioUtc = now;

        if (ctx.RouterRunning && ctx.OutputDevice is not null && !ctx.OutputOk)
        {
            ShowHint("output_disconnected", $"Output device is disconnected ({ctx.OutputDevice}).");
            return;
        }

        if (ctx.RouterRunning && (now - _lastGameAudioUtc).TotalSeconds >= 3)
        {
            ShowHint("game_silent", "No audio detected from the selected Game Source for 3 seconds.");
            return;
        }

        if (ctx.RouterRunning && ctx.ShakerEnabled)
        {
            // Shaker muted/zeroed: routing says shaker is enabled, but LFE isn't moving.
            if (ctx.GamePeak > 0.01f && (now - _lastLfeAudioUtc).TotalSeconds >= 3 && ctx.ShakerStrengthDb > -30)
            {
                ShowHint("lfe_zero", "Shaker routing is enabled, but LFE output looks silent.");
                return;
            }

            // Clipping detection: treat near-full-scale as a warning.
            if (ctx.OutputLfePeak > 0.95f)
            {
                ShowHint("lfe_clip", "LFE output is close to clipping. Consider reducing shaker strength.");
                return;
            }
        }

        if (ctx.RouterRunning && !ctx.ShakerEnabled && ctx.GamePeak > 0.01f)
        {
            ShowHint("shaker_disabled", "Bass Shaker routing is disabled.");
            return;
        }

        if (ctx.RouterRunning && ctx.ShakerEnabled && ctx.ShakerStrengthDb <= -50)
        {
            ShowHint("shaker_low", "Bass Shaker is enabled but strength is very low.");
            return;
        }

        HideHint();
    }

    private void ShowHint(string key, string text)
    {
        _activeHintKey = key;
        _hintText.Text = text;
        _hintBar.Visible = true;
    }

    private void HideHint()
    {
        _hintBar.Visible = false;
        _hintText.Text = "";
        _activeHintKey = null;
    }

    private void OnHintHelpClicked()
    {
        var ctx = GetContext?.Invoke();
        if (ctx is null)
            return;

        // Single-cause, single-fix pattern.
        if (_activeHintKey == "game_silent")
        {
            Append("Not detecting audio from Game Source. Common cause: the selected device is not where your game is playing.");
            Append("Fix: switch Game Source to 'Default Game Output' or pick the correct device.");
            var actions = new List<CopilotAction>();
            actions.Add(new CopilotAction("set_game_source", StringValue: DeviceHelper.DefaultSystemRenderDevice));
            ApplyActionsWithConfirmation?.Invoke(actions.ToArray());
        }
        else if (_activeHintKey == "output_disconnected")
        {
            Append("Output device looks disconnected.");
            Append("Fix: refresh devices and re-select output.");
            ApplyActionsWithConfirmation?.Invoke([
                new CopilotAction("refresh_devices")
            ]);
        }
        else if (_activeHintKey == "lfe_zero")
        {
            Append("LFE output looks silent while routing is active.");
            Append("Fix: increase shaker strength by +6 dB (you can undo anytime).");
            ApplyActionsWithConfirmation?.Invoke([
                new CopilotAction("set_shaker_strength_db", FloatValue: Math.Min(12f, ctx.ShakerStrengthDb + 6f))
            ]);
        }
        else if (_activeHintKey == "lfe_clip")
        {
            Append("LFE is close to clipping.");
            Append("Fix: reduce shaker strength by -3 dB.");
            ApplyActionsWithConfirmation?.Invoke([
                new CopilotAction("set_shaker_strength_db", FloatValue: Math.Max(-24f, ctx.ShakerStrengthDb - 3f))
            ]);
        }
        else if (_activeHintKey == "shaker_disabled")
        {
            Append("Bass Shaker routing is disabled.");
            Append("Fix: enable shaker from Game Source.");
            ApplyActionsWithConfirmation?.Invoke([
                new CopilotAction("set_shaker_mode", StringValue: "gamesOnly")
            ]);
        }
    }

    private void StartFirstRunIfNeeded()
    {
        if (!_settings.Enabled)
            return;
        if (_settings.HasSeenFirstRunPrompt)
            return;

        _stage = WizardStage.OfferHelp;
        RebuildActions();
        Append("I can help you set this up in under 2 minutes. Want help?");
    }

    private void RebuildActions()
    {
        _actions.Controls.Clear();

        var help = new Button { Text = "Help me set this up", Width = 170, Height = 30 };
        help.Click += (_, _) =>
        {
            _stage = WizardStage.OfferHelp;
            RebuildActions();
            Append("I can help you set this up in under 2 minutes. Want help?");
        };
        _actions.Controls.Add(help);

        if (_stage == WizardStage.OfferHelp)
        {
            var yes = new Button { Text = "Yes", Width = 80, Height = 30 };
            var skip = new Button { Text = "Skip", Width = 80, Height = 30 };

            yes.Click += (_, _) =>
            {
                _stage = WizardStage.Purpose;
                RebuildActions();
                Append("What are you using this for?");
            };

            skip.Click += (_, _) =>
            {
                _settings = _settings with { HasSeenFirstRunPrompt = true };
                SaveSettings?.Invoke(_settings);
                _stage = WizardStage.None;
                RebuildActions();
                Append("Ok. You can open this assistant anytime.");
            };

            _actions.Controls.Add(yes);
            _actions.Controls.Add(skip);
            return;
        }

        if (_stage == WizardStage.Purpose)
        {
            AddChoiceButton("Games + bass shaker", "games");
            AddChoiceButton("Music only", "music");
            AddChoiceButton("Experimenting", "experiment");
            return;
        }

        if (_stage == WizardStage.OutputDevice)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
            combo.Items.AddRange(_outputDevices.Cast<object>().ToArray());
            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;

            var next = new Button { Text = "Next", Width = 80, Height = 30 };
            next.Click += (_, _) =>
            {
                _chosenOutput = combo.SelectedItem as string;
                _stage = WizardStage.ShakerMode;
                RebuildActions();
                Append("Do you want shaker effects always on, or only for games?");
            };

            _actions.Controls.Add(combo);
            _actions.Controls.Add(next);
            return;
        }

        if (_stage == WizardStage.ShakerMode)
        {
            var always = new Button { Text = "Always on", Width = 110, Height = 30 };
            var gamesOnly = new Button { Text = "Only for games", Width = 130, Height = 30 };

            always.Click += (_, _) =>
            {
                _shakerMode = "always";
                _stage = WizardStage.Confirm;
                RebuildActions();
                Append("I will apply a recommended setup. You can change it anytime.");
            };
            gamesOnly.Click += (_, _) =>
            {
                _shakerMode = "gamesOnly";
                _stage = WizardStage.Confirm;
                RebuildActions();
                Append("I will apply a recommended setup. You can change it anytime.");
            };

            _actions.Controls.Add(always);
            _actions.Controls.Add(gamesOnly);
            return;
        }

        if (_stage == WizardStage.Confirm)
        {
            var show = new Button { Text = "Show me first", Width = 120, Height = 30 };
            var apply = new Button { Text = "Apply", Width = 80, Height = 30 };

            show.Click += (_, _) => Append(BuildPlanText(previewOnly: true));
            apply.Click += (_, _) =>
            {
                var actions = BuildPlanActions();
                ApplyActionsWithConfirmation?.Invoke(actions);

                _settings = _settings with { HasSeenFirstRunPrompt = true };
                SaveSettings?.Invoke(_settings);
                _stage = WizardStage.None;
                RebuildActions();
            };

            _actions.Controls.Add(show);
            _actions.Controls.Add(apply);
            return;
        }
    }

    private void AddChoiceButton(string text, string purpose)
    {
        var b = new Button { Text = text, Width = 170, Height = 30 };
        b.Click += (_, _) =>
        {
            _purpose = purpose;
            _stage = WizardStage.OutputDevice;
            RebuildActions();
            Append("Which device is your bass shaker connected to?");
        };
        _actions.Controls.Add(b);
    }

    private string BuildPlanText(bool previewOnly)
    {
        var sb = new StringBuilder();
        sb.AppendLine(previewOnly ? "Plan (preview):" : "Plan:");

        if (!string.IsNullOrWhiteSpace(_chosenOutput))
            sb.AppendLine($"- Set output device: {_chosenOutput}");

        if (_purpose == "music")
            sb.AppendLine("- Apply preset: Game Only");
        else if (_purpose == "experiment")
            sb.AppendLine("- Enable Advanced Controls");
        else
            sb.AppendLine("- Apply preset: Game + Bass Shaker");

        if (_purpose != "experiment")
        {
            if (_shakerMode == "gamesOnly")
                sb.AppendLine("- Route shaker from Game Source only");
            else
                sb.AppendLine("- Route shaker from all sources");
        }

        sb.AppendLine("Nothing changes until you click Apply.");
        return sb.ToString().TrimEnd();
    }

    private CopilotAction[] BuildPlanActions()
    {
        var actions = new List<CopilotAction>();
        if (!string.IsNullOrWhiteSpace(_chosenOutput))
            actions.Add(new CopilotAction("set_output_device", StringValue: _chosenOutput));

        if (_purpose == "music")
        {
            actions.Add(new CopilotAction("apply_simple_preset", StringValue: "Game Only"));
            actions.Add(new CopilotAction("set_shaker_mode", StringValue: _shakerMode ?? "always"));
        }
        else if (_purpose == "experiment")
        {
            actions.Add(new CopilotAction("show_advanced_controls", BoolValue: true));
        }
        else
        {
            actions.Add(new CopilotAction("apply_simple_preset", StringValue: "Game + Bass Shaker"));
            actions.Add(new CopilotAction("set_shaker_mode", StringValue: _shakerMode ?? "always"));
        }
        return actions.ToArray();
    }

    private void Append(string text)
    {
        if (_log.TextLength > 0) _log.AppendText(Environment.NewLine + Environment.NewLine);
        _log.AppendText(text);
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }
}
