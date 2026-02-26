using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cm6206DualRouter;

public sealed class RouterConfig
{
    // Input ingestion mode:
    // - WasapiLoopback (current): capture from render endpoints via WASAPI loopback
    // - CmvadrIoctl (planned): pull PCM from the SysVAD/WaveRT virtual driver via IOCTL (no loopback)
    [JsonPropertyName("inputIngestMode")]
    public string InputIngestMode { get; set; } = "WasapiLoopback";

    [JsonPropertyName("musicInputRenderDevice")]
    public string MusicInputRenderDevice { get; set; } = string.Empty;

    [JsonPropertyName("shakerInputRenderDevice")]
    public string ShakerInputRenderDevice { get; set; } = string.Empty;

    // Optional: generate Shaker audio in-process by consuming the mod's external telemetry stream
    // (loopback WebSocket server that broadcasts JSON frames).
    // When enabled, the Shaker stream is synthesized. If ShakerInputRenderDevice is also set,
    // it becomes the automatic fallback when telemetry is not being received.
    [JsonPropertyName("telemetryHapticsEnabled")]
    public bool TelemetryHapticsEnabled { get; set; } = false;

    [JsonPropertyName("telemetryWebSocketHost")]
    public string TelemetryWebSocketHost { get; set; } = "127.0.0.1";

    [JsonPropertyName("telemetryWebSocketPort")]
    public int TelemetryWebSocketPort { get; set; } = 7117;

    // Which message types to consume from the mod's JSON broadcast.
    // - telemetry: continuous (speed/accel/elytra) updates
    // - event: high-level one-shot events (impact/danger/etc.)
    // - haptic: low-level synthesis commands (f0/f1/ms/noise/pattern)
    [JsonPropertyName("telemetryConsumeTelemetry")]
    public bool TelemetryConsumeTelemetry { get; set; } = true;

    [JsonPropertyName("telemetryConsumeUnifiedEvents")]
    public bool TelemetryConsumeUnifiedEvents { get; set; } = true;

    [JsonPropertyName("telemetryConsumeHapticCommands")]
    public bool TelemetryConsumeHapticCommands { get; set; } = false;

    // Additional gain applied only to the synthesized telemetry shaker stream.
    [JsonPropertyName("telemetryGainDb")]
    public float TelemetryGainDb { get; set; } = 0.0f;

    [JsonPropertyName("outputRenderDevice")]
    public string OutputRenderDevice { get; set; } = string.Empty;

    // Optional: a microphone/line-in capture device used for round-trip latency measurement.
    [JsonPropertyName("latencyInputCaptureDevice")]
    public string? LatencyInputCaptureDevice { get; set; } = null;

    [JsonPropertyName("sampleRate")]
    public int SampleRate { get; set; } = 48000;

    // Optional: exclusive-mode sample rates to avoid (some adapters glitch at 192kHz, etc.).
    [JsonPropertyName("blacklistedSampleRates")]
    public int[]? BlacklistedSampleRates { get; set; } = null;

    [JsonPropertyName("outputChannels")]
    public int OutputChannels { get; set; } = 8;

    [JsonPropertyName("musicGainDb")]
    public float MusicGainDb { get; set; } = 0.0f;

    [JsonPropertyName("shakerGainDb")]
    public float ShakerGainDb { get; set; } = 0.0f;

    // Global gain applied to all output channels after routing/mapping.
    [JsonPropertyName("masterGainDb")]
    public float MasterGainDb { get; set; } = 0.0f;

    [JsonPropertyName("shakerHighPassHz")]
    public float ShakerHighPassHz { get; set; } = 20.0f;

    [JsonPropertyName("shakerLowPassHz")]
    public float ShakerLowPassHz { get; set; } = 80.0f;

    // Mixing strategy for how inputs are combined.
    // - FrontBoth: Front L/R = Music + Shaker (default)
    // - Dedicated: Front L/R = Music only; Shaker stays on Rear/Side/LFE
    // - MusicOnly: Output contains only Music (shaker muted everywhere)
    // - ShakerOnly: Output contains only Shaker (music muted everywhere)
    // - PriorityMusic: Switch between MusicOnly/ShakerOnly, biasing toward Music
    // - PriorityShaker: Switch between MusicOnly/ShakerOnly, biasing toward Shaker
    [JsonPropertyName("mixingMode")]
    public string MixingMode { get; set; } = "FrontBoth";

    // Optional: explicit routing matrix (6 rows x 2 cols) that overrides mixingMode.
    // Row order: Front, Center, LFE, Rear, Side, Reserved.
    // Col order: A (Music), B (Shaker).
    // Stored row-major: index = row*2 + col.
    [JsonPropertyName("groupRouting")]
    public bool[]? GroupRouting { get; set; } = null;

    [JsonPropertyName("musicHighPassHz")]
    public float? MusicHighPassHz { get; set; } = null;

    [JsonPropertyName("musicLowPassHz")]
    public float? MusicLowPassHz { get; set; } = null;

    [JsonPropertyName("lfeGainDb")]
    public float LfeGainDb { get; set; } = -6.0f;

    [JsonPropertyName("rearGainDb")]
    public float RearGainDb { get; set; } = 0.0f;

    [JsonPropertyName("sideGainDb")]
    public float SideGainDb { get; set; } = 0.0f;

    [JsonPropertyName("useCenterChannel")]
    public bool UseCenterChannel { get; set; } = false;

    [JsonPropertyName("useExclusiveMode")]
    public bool UseExclusiveMode { get; set; } = false;

    [JsonPropertyName("enableVoicePrompts")]
    public bool EnableVoicePrompts { get; set; } = true;

    [JsonPropertyName("calibrationAutoStep")]
    public bool CalibrationAutoStep { get; set; } = false;

    // One of: Manual, IdentifySine, LevelPink, AlternateSinePink
    [JsonPropertyName("calibrationPreset")]
    public string CalibrationPreset { get; set; } = "Manual";

    [JsonPropertyName("calibrationStepMs")]
    public int CalibrationStepMs { get; set; } = 2000;

    [JsonPropertyName("calibrationLoop")]
    public bool CalibrationLoop { get; set; } = true;

    // Per-output-channel trims (dB). Order is WAVEFORMATEXTENSIBLE 7.1:
    // FL, FR, FC, LFE, BL, BR, SL, SR
    // If null, defaults to 0 dB on all channels.
    [JsonPropertyName("channelGainsDb")]
    public float[]? ChannelGainsDb { get; set; } = null;

    // Output channel mapping (indices 0..7). Each output channel picks a source channel.
    // Default is identity: [0,1,2,3,4,5,6,7]
    [JsonPropertyName("outputChannelMap")]
    public int[]? OutputChannelMap { get; set; } = null;

    // Per-output-channel mute / phase invert. If null, defaults to false for all.
    [JsonPropertyName("channelMute")]
    public bool[]? ChannelMute { get; set; } = null;

    [JsonPropertyName("channelInvert")]
    public bool[]? ChannelInvert { get; set; } = null;

    [JsonPropertyName("channelSolo")]
    public bool[]? ChannelSolo { get; set; } = null;

    [JsonPropertyName("latencyMs")]
    public int LatencyMs { get; set; } = 50;

    public static RouterConfig Load(string path, bool validate = true)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Config not found: {path}");
        }

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var config = JsonSerializer.Deserialize<RouterConfig>(json, options) ??
                     throw new InvalidOperationException("Failed to parse config.");

        if (validate)
            config.Validate();
        return config;
    }

    public void Validate(bool requireDevices = true)
    {
        var ingest = (InputIngestMode ?? "WasapiLoopback").Trim();
        if (ingest is not ("WasapiLoopback" or "CmvadrIoctl"))
            throw new InvalidOperationException("inputIngestMode must be one of: WasapiLoopback, CmvadrIoctl");

        if (requireDevices)
        {
            if (string.IsNullOrWhiteSpace(MusicInputRenderDevice))
                throw new InvalidOperationException("musicInputRenderDevice is required");
            if (string.IsNullOrWhiteSpace(OutputRenderDevice))
                throw new InvalidOperationException("outputRenderDevice is required");
        }

        if (TelemetryHapticsEnabled)
        {
            if (string.IsNullOrWhiteSpace(TelemetryWebSocketHost))
                throw new InvalidOperationException("telemetryWebSocketHost is required when telemetryHapticsEnabled=true");
            if (TelemetryWebSocketPort <= 0 || TelemetryWebSocketPort > 65535)
                throw new InvalidOperationException("telemetryWebSocketPort is out of range (1..65535)");
        }
        if (SampleRate < 8000 || SampleRate > 384000)
            throw new InvalidOperationException("sampleRate is out of range");

        if (BlacklistedSampleRates is not null)
        {
            foreach (var sr in BlacklistedSampleRates)
            {
                if (sr < 8000 || sr > 384000)
                    throw new InvalidOperationException("blacklistedSampleRates contains an out-of-range value");
            }
        }
        if (OutputChannels != 8)
            throw new InvalidOperationException("This build currently expects outputChannels=8 (7.1)");
        if (LatencyMs < 10 || LatencyMs > 500)
            throw new InvalidOperationException("latencyMs is out of range (10..500)");

        if (CalibrationStepMs < 250 || CalibrationStepMs > 30000)
            throw new InvalidOperationException("calibrationStepMs is out of range (250..30000)");

        var preset = (CalibrationPreset ?? "Manual").Trim();
        if (preset is not ("Manual" or "IdentifySine" or "LevelPink" or "AlternateSinePink"))
            throw new InvalidOperationException("calibrationPreset must be one of: Manual, IdentifySine, LevelPink, AlternateSinePink");
        if (ShakerHighPassHz <= 0 || ShakerLowPassHz <= 0 || ShakerHighPassHz >= ShakerLowPassHz)
            throw new InvalidOperationException("shakerHighPassHz must be >0 and < shakerLowPassHz");

        if (MasterGainDb < -120 || MasterGainDb > 24)
            throw new InvalidOperationException("masterGainDb is out of range (-120..24)");

        var mode = (MixingMode ?? "FrontBoth").Trim();
        if (mode is not ("FrontBoth" or "Dedicated" or "MusicOnly" or "ShakerOnly" or "PriorityMusic" or "PriorityShaker"))
            throw new InvalidOperationException("mixingMode must be one of: FrontBoth, Dedicated, MusicOnly, ShakerOnly, PriorityMusic, PriorityShaker");

        if (ChannelGainsDb is not null && ChannelGainsDb.Length != 8)
            throw new InvalidOperationException("channelGainsDb must be an array of 8 floats (FL,FR,FC,LFE,BL,BR,SL,SR)");

        if (OutputChannelMap is not null)
        {
            if (OutputChannelMap.Length != 8)
                throw new InvalidOperationException("outputChannelMap must be an array of 8 ints");
            for (var i = 0; i < 8; i++)
            {
                if (OutputChannelMap[i] < 0 || OutputChannelMap[i] > 7)
                    throw new InvalidOperationException("outputChannelMap values must be in range 0..7");
            }
        }

        if (ChannelMute is not null && ChannelMute.Length != 8)
            throw new InvalidOperationException("channelMute must be an array of 8 bools");
        if (ChannelInvert is not null && ChannelInvert.Length != 8)
            throw new InvalidOperationException("channelInvert must be an array of 8 bools");
        if (ChannelSolo is not null && ChannelSolo.Length != 8)
            throw new InvalidOperationException("channelSolo must be an array of 8 bools");

        if (MusicHighPassHz is not null)
        {
            if (MusicHighPassHz <= 0 || MusicHighPassHz > 300)
                throw new InvalidOperationException("musicHighPassHz is out of range (0..300]");
        }

        if (MusicLowPassHz is not null)
        {
            if (MusicLowPassHz <= 0 || MusicLowPassHz > 300)
                throw new InvalidOperationException("musicLowPassHz is out of range (0..300]");
        }

        if (MusicHighPassHz is not null && MusicLowPassHz is not null)
        {
            if (MusicHighPassHz <= 0 || MusicLowPassHz <= 0 || MusicHighPassHz >= MusicLowPassHz)
                throw new InvalidOperationException("musicHighPassHz must be >0 and < musicLowPassHz");
        }

        if (GroupRouting is not null && GroupRouting.Length != 12)
            throw new InvalidOperationException("groupRouting must be an array of 12 bools (6x2 matrix)");
    }

    public RouterConfig Clone()
    {
        var c = (RouterConfig)MemberwiseClone();
        c.BlacklistedSampleRates = BlacklistedSampleRates?.ToArray();
        c.ChannelGainsDb = ChannelGainsDb?.ToArray();
        c.OutputChannelMap = OutputChannelMap?.ToArray();
        c.ChannelMute = ChannelMute?.ToArray();
        c.ChannelSolo = ChannelSolo?.ToArray();
        c.ChannelInvert = ChannelInvert?.ToArray();
        c.GroupRouting = GroupRouting?.ToArray();
        return c;
    }
}
