using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

public class PixelRectangleTests
{
    [Fact]
    public void Constructor_SetsAllFields()
    {
        var rect = new PixelRectangle(1, 2, 3, 4);

        rect.X.ShouldBe(1);
        rect.Y.ShouldBe(2);
        rect.Width.ShouldBe(3);
        rect.Height.ShouldBe(4);
    }
}
