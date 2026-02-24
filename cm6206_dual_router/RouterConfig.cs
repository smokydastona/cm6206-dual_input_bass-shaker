using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cm6206DualRouter;

public sealed class RouterConfig
{
    [JsonPropertyName("musicInputRenderDevice")]
    public string MusicInputRenderDevice { get; set; } = string.Empty;

    [JsonPropertyName("shakerInputRenderDevice")]
    public string ShakerInputRenderDevice { get; set; } = string.Empty;

    [JsonPropertyName("outputRenderDevice")]
    public string OutputRenderDevice { get; set; } = string.Empty;

    [JsonPropertyName("sampleRate")]
    public int SampleRate { get; set; } = 48000;

    [JsonPropertyName("outputChannels")]
    public int OutputChannels { get; set; } = 8;

    [JsonPropertyName("musicGainDb")]
    public float MusicGainDb { get; set; } = 0.0f;

    [JsonPropertyName("shakerGainDb")]
    public float ShakerGainDb { get; set; } = 0.0f;

    [JsonPropertyName("shakerHighPassHz")]
    public float ShakerHighPassHz { get; set; } = 20.0f;

    [JsonPropertyName("shakerLowPassHz")]
    public float ShakerLowPassHz { get; set; } = 80.0f;

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

    [JsonPropertyName("latencyMs")]
    public int LatencyMs { get; set; } = 50;

    public static RouterConfig Load(string path)
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

        config.Validate();
        return config;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MusicInputRenderDevice))
            throw new InvalidOperationException("musicInputRenderDevice is required");
        if (string.IsNullOrWhiteSpace(ShakerInputRenderDevice))
            throw new InvalidOperationException("shakerInputRenderDevice is required");
        if (string.IsNullOrWhiteSpace(OutputRenderDevice))
            throw new InvalidOperationException("outputRenderDevice is required");
        if (SampleRate < 8000 || SampleRate > 384000)
            throw new InvalidOperationException("sampleRate is out of range");
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

        if (MusicHighPassHz is not null && MusicLowPassHz is not null)
        {
            if (MusicHighPassHz <= 0 || MusicLowPassHz <= 0 || MusicHighPassHz >= MusicLowPassHz)
                throw new InvalidOperationException("musicHighPassHz must be >0 and < musicLowPassHz");
        }
    }
}
