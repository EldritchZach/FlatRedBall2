namespace FlatRedBall2.Audio;

/// <summary>Pure PCM conversion helpers shared by streaming audio backends.</summary>
internal static class AudioSampleConversion
{
    /// <summary>
    /// Converts <paramref name="count"/> samples from <paramref name="samples"/> (expected range
    /// [-1, 1], clamped) into little-endian 16-bit PCM bytes, two bytes per sample.
    /// </summary>
    internal static byte[] FloatSamplesToPcm16(float[] samples, int count)
    {
        var pcm = new byte[count * 2];

        for (int i = 0; i < count; i++)
        {
            float clamped = System.Math.Clamp(samples[i], -1f, 1f);
            // Asymmetric scale (negative side uses the extra magnitude down to short.MinValue)
            // is the conventional float-to-PCM16 mapping and uses the full 16-bit range.
            short sample = (short)(clamped * (clamped < 0 ? -(float)short.MinValue : short.MaxValue));
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return pcm;
    }
}
