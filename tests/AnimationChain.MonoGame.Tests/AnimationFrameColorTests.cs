using FlatRedBall.AnimationChain;
using Microsoft.Xna.Framework;
using Xunit;

namespace AnimationChain.MonoGame.Tests;

public class AnimationFrameColorTests
{
    [Fact]
    public void Apply_NoColorOperation_ReturnsBaseColorUnchanged()
    {
        var frame = new AnimationFrame { Red = 128, Green = 10, Blue = 10 };

        var result = AnimationFrameColor.Apply(frame, Color.White);

        Assert.Equal(Color.White, result);
    }

    [Fact]
    public void Apply_Multiply_ScalesBaseColorByChannels()
    {
        var frame = new AnimationFrame
        {
            ColorOperation = ColorOperation.Multiply,
            Red = 128,
            Green = 255,
            Blue = 0,
        };

        var result = AnimationFrameColor.Apply(frame, Color.White);

        Assert.Equal(128, result.R);
        Assert.Equal(255, result.G);
        Assert.Equal(0, result.B);
    }

    [Fact]
    public void Apply_MultiplyWithUnsetChannel_UsesIdentityDefault()
    {
        // Blue never authored -> identity for Multiply is 255 (no change).
        var frame = new AnimationFrame
        {
            ColorOperation = ColorOperation.Multiply,
            Red = 0,
        };

        var result = AnimationFrameColor.Apply(frame, new Color(200, 200, 200, 255));

        Assert.Equal(0, result.R);
        Assert.Equal(200, result.G);
        Assert.Equal(200, result.B);
    }

    [Fact]
    public void Apply_MultiplyWithNegativeChannel_ClampsToZeroInsteadOfWrapping()
    {
        var frame = new AnimationFrame
        {
            ColorOperation = ColorOperation.Multiply,
            Red = -50,
        };

        var result = AnimationFrameColor.Apply(frame, Color.White);

        Assert.Equal(0, result.R);
    }

    [Fact]
    public void Apply_AlphaSet_AppliesIndependentlyOfColorOperation()
    {
        var frame = new AnimationFrame { Alpha = 128 };

        var result = AnimationFrameColor.Apply(frame, Color.White);

        Assert.Equal(128, result.A);
        Assert.Equal(255, result.R);
    }

    [Fact]
    public void Apply_AddOperation_DoesNotChangeRgb()
    {
        // Add is applied via a pixel shader (see GetAddOffset), not the vertex color - RGB passes through here.
        var frame = new AnimationFrame
        {
            ColorOperation = ColorOperation.Add,
            Red = 200,
        };

        var result = AnimationFrameColor.Apply(frame, Color.White);

        Assert.Equal(Color.White.R, result.R);
    }

    [Fact]
    public void GetAddOffset_UnsetChannels_DefaultToZero()
    {
        var frame = new AnimationFrame { ColorOperation = ColorOperation.Add, Red = 128 };

        var offset = AnimationFrameColor.GetAddOffset(frame);

        Assert.Equal(128 / 255f, offset.X);
        Assert.Equal(0f, offset.Y);
        Assert.Equal(0f, offset.Z);
    }

    [Fact]
    public void GetAddOffset_NegativeChannel_ClampsToAuthoredRangeInsteadOfWrapping()
    {
        var frame = new AnimationFrame { ColorOperation = ColorOperation.Add, Red = -500 };

        var offset = AnimationFrameColor.GetAddOffset(frame);

        Assert.Equal(-1f, offset.X);
    }
}
