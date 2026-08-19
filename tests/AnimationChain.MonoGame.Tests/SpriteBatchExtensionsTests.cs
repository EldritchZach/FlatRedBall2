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
}
