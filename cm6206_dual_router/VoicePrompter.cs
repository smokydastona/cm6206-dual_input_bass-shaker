using System.Speech.Synthesis;

namespace Cm6206DualRouter;

public static class VoicePrompter
{
    private static readonly object Gate = new();
    private static SpeechSynthesizer? _synth;

    public static void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            lock (Gate)
            {
                _synth ??= new SpeechSynthesizer();
                _synth.SpeakAsyncCancelAll();
                _synth.SpeakAsync(text);
            }
        }
        catch
        {
            // If TTS isn't available on the system, silently ignore.
        }
    }

    public static void Dispose()
    {
        lock (Gate)
        {
            try { _synth?.Dispose(); } catch { /* ignore */ }
            _synth = null;
        }
    }
}
