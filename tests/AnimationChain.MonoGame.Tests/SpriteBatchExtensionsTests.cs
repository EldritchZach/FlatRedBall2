using FlatRedBall.AnimationChain;
using Microsoft.Xna.Framework;
using Xunit;

namespace AnimationChain.MonoGame.Tests;

public class SpriteBatchExtensionsTests
{
    [Fact]
    public void ResolveBaseColor_NoColorGiven_DefaultsToWhite()
    {
        var result = SpriteBatchExtensions.ResolveBaseColor(null);

        Assert.Equal(Color.White, result);
    }

    [Fact]
    public void ResolveBaseColor_ColorGiven_ReturnsThatColorUnchanged()
    {
        var result = SpriteBatchExtensions.ResolveBaseColor(Color.Red);

        Assert.Equal(Color.Red, result);
    }

    [Fact]
    public void ResolveOrigin_NoOriginGiven_DefaultsToCenter()
    {
        var result = SpriteBatchExtensions.ResolveOrigin(null, width: 32, height: 16);

        Assert.Equal(new Vector2(16f, 8f), result);
    }

    [Fact]
    public void ResolveOrigin_OriginGiven_ReturnsThatOriginUnchanged()
    {
        var result = SpriteBatchExtensions.ResolveOrigin(Vector2.Zero, width: 32, height: 16);

        Assert.Equal(Vector2.Zero, result);
    }
}
