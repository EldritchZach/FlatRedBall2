using FlatRedBall.AnimationChain;
using FlatRedBall2.AnimationEditorCommon;
using Microsoft.Xna.Framework;
using Xunit;

namespace AnimationChain.MonoGame.Tests;

public class PixelRectangleExtensionsTests
{
    [Fact]
    public void ToXnaRectangle_CopiesFieldsDirectly()
    {
        var pixelRect = new PixelRectangle(1, 2, 3, 4);

        var xnaRect = pixelRect.ToXnaRectangle();

        Assert.Equal(new Rectangle(1, 2, 3, 4), xnaRect);
    }
}
