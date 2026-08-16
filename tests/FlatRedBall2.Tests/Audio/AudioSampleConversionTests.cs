using FlatRedBall2.Audio;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Audio;

public class AudioSampleConversionTests
{
    [Fact]
    public void FloatSamplesToPcm16_ClampsOutOfRangeValues_ToShortMinAndMax()
    {
        float[] samples = [-2f, 2f];

        byte[] pcm = AudioSampleConversion.FloatSamplesToPcm16(samples, samples.Length);

        short min = (short)(pcm[0] | (pcm[1] << 8));
        short max = (short)(pcm[2] | (pcm[3] << 8));
        min.ShouldBe(short.MinValue);
        max.ShouldBe(short.MaxValue);
    }

    [Fact]
    public void FloatSamplesToPcm16_MidRangeValue_PacksLittleEndian16BitSample()
    {
        float[] samples = [0.5f];
        short expected = (short)(0.5f * short.MaxValue);

        byte[] pcm = AudioSampleConversion.FloatSamplesToPcm16(samples, samples.Length);

        pcm.Length.ShouldBe(2);
        pcm[0].ShouldBe((byte)(expected & 0xFF));
        pcm[1].ShouldBe((byte)((expected >> 8) & 0xFF));
    }
}
