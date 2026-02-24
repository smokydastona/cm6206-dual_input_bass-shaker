using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Cm6206DualRouter;

public static class LatencyMeasurer
{
    public sealed record Result(double EstimatedMs, int CaptureSampleRate, int CapturedSamples);

    public static Task<Result> MeasureAsync(RouterConfig config, string captureDeviceFriendlyName, CancellationToken cancellationToken)
    {
        return Task.Run(() => Measure(config, captureDeviceFriendlyName, cancellationToken), cancellationToken);
    }

    private static Result Measure(RouterConfig config, string captureDeviceFriendlyName, CancellationToken cancellationToken)
    {
        const int preRollMs = 200;
        const int recordMs = 1200;

        using var outputDevice = DeviceHelper.GetRenderDeviceByFriendlyName(config.OutputRenderDevice);
        using var captureDevice = DeviceHelper.GetCaptureDeviceByFriendlyName(captureDeviceFriendlyName);

        using var capture = new WasapiCapture(captureDevice);

        var captured = new List<float>(capture.WaveFormat.SampleRate * 2);
        capture.DataAvailable += (_, e) =>
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            // Convert to mono float samples (avg channels).
            var wf = capture.WaveFormat;
            var channels = Math.Max(1, wf.Channels);

            if (wf.Encoding == WaveFormatEncoding.IeeeFloat && wf.BitsPerSample == 32)
            {
                var bytesPerSample = 4;
                var frames = e.BytesRecorded / (bytesPerSample * channels);
                var wb = new WaveBuffer(e.Buffer);
                var floats = wb.FloatBuffer;

                for (var f = 0; f < frames; f++)
                {
                    var sum = 0f;
                    var baseIndex = f * channels;
                    for (var ch = 0; ch < channels; ch++)
                        sum += floats[baseIndex + ch];
                    captured.Add(sum / channels);
                }

                return;
            }

            if (wf.Encoding == WaveFormatEncoding.Pcm && wf.BitsPerSample == 16)
            {
                var bytesPerSample = 2;
                var frames = e.BytesRecorded / (bytesPerSample * channels);
                var wb = new WaveBuffer(e.Buffer);
                var shorts = wb.ShortBuffer;

                for (var f = 0; f < frames; f++)
                {
                    var sum = 0f;
                    var baseIndex = f * channels;
                    for (var ch = 0; ch < channels; ch++)
                        sum += shorts[baseIndex + ch] / 32768f;
                    captured.Add(sum / channels);
                }

                return;
            }

            // Fallback: ignore unsupported formats.
        };

        using var output = CreateOutput(config, outputDevice);

        var format = WaveFormatFactory.Create7Point1Float(config.SampleRate);
        var click = new ClickSampleProvider(format)
        {
            ChannelIndex = 0,
            BurstFrequencyHz = 1000f,
            BurstMs = 12,
            LevelDb = -6f
        };

        output.Init(new SampleToWaveProvider(click));

        capture.StartRecording();
        Thread.Sleep(preRollMs);

        output.Play();
        Thread.Sleep(recordMs);

        output.Stop();
        capture.StopRecording();

        if (captured.Count == 0)
            throw new InvalidOperationException("No audio captured. Check the capture device and permissions.");

        // Find the biggest peak.
        var peakIndex = 0;
        var peak = 0f;
        for (var i = 0; i < captured.Count; i++)
        {
            var v = MathF.Abs(captured[i]);
            if (v > peak)
            {
                peak = v;
                peakIndex = i;
            }
        }

        // Convert peak location to ms relative to when we started playback.
        var captureRate = capture.WaveFormat.SampleRate;
        var preRollSamples = (int)(captureRate * (preRollMs / 1000.0));
        var delaySamples = peakIndex - preRollSamples;
        if (delaySamples < 0) delaySamples = 0;

        var estimatedMs = delaySamples * 1000.0 / captureRate;
        return new Result(estimatedMs, captureRate, captured.Count);
    }

    private static WasapiOut CreateOutput(RouterConfig config, MMDevice device)
    {
        var shareMode = config.UseExclusiveMode ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared;
        return new WasapiOut(device, shareMode, true, config.LatencyMs);
    }
}
