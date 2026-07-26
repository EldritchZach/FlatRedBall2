using FlatRedBall2.Animation;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

public class SpriteFrameColorTests
{
    [Fact]
    public void Apply_Multiply_ScalesBaseColorByChannels()
    {
        var frame = new AnimationFrame { ColorOperation = ColorOperation.Multiply, Red = 128, Green = 255, Blue = 0 };

        var result = SpriteFrameColor.Apply(frame, Color.White);

        result.R.ShouldBe((byte)128);
        result.G.ShouldBe((byte)255);
        result.B.ShouldBe((byte)0);
    }

    [Fact]
    public void Apply_AddOperation_DoesNotChangeRgb()
    {
        // Add is applied via the pixel shader (see GetAddOffset), not the vertex color.
        var frame = new AnimationFrame { ColorOperation = ColorOperation.Add, Red = 200 };

        var result = SpriteFrameColor.Apply(frame, Color.White);

        result.R.ShouldBe(Color.White.R);
    }

    [Fact]
    public void GetAddOffset_UnsetChannels_DefaultToZero()
    {
        var frame = new AnimationFrame { ColorOperation = ColorOperation.Add, Red = 128 };

        var offset = SpriteFrameColor.GetAddOffset(frame);

        offset.X.ShouldBe(128 / 255f, tolerance: 0.0001f);
        offset.Y.ShouldBe(0f);
        offset.Z.ShouldBe(0f);
    }

    [Fact]
    public void GetAddOffset_NegativeChannel_ClampsToAuthoredRangeInsteadOfWrapping()
    {
        var frame = new AnimationFrame { ColorOperation = ColorOperation.Add, Red = -500 };

        var offset = SpriteFrameColor.GetAddOffset(frame);

        offset.X.ShouldBe(-1f);
    }
}
