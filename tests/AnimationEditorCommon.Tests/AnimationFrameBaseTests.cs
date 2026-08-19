using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

public class AnimationFrameBaseTests
{
    // Concrete stand-in — AnimationFrameBase itself is abstract with no renderer-specific data.
    private class TestFrame : AnimationFrameBase { }

    [Fact]
    public void Constructor_DefaultValues_AreEmpty()
    {
        var frame = new TestFrame();

        frame.TextureName.ShouldBe(string.Empty);
        frame.FrameLength.ShouldBe(TimeSpan.Zero);
        frame.FlipHorizontal.ShouldBeFalse();
        frame.FlipVertical.ShouldBeFalse();
        frame.FlipDiagonal.ShouldBeFalse();
        frame.RelativeX.ShouldBe(0f);
        frame.RelativeY.ShouldBe(0f);
        frame.Red.ShouldBeNull();
        frame.Green.ShouldBeNull();
        frame.Blue.ShouldBeNull();
        frame.Alpha.ShouldBeNull();
        frame.ColorOperation.ShouldBeNull();
        frame.SourceRectangle.ShouldBeNull();
        frame.Shapes.ShouldBeEmpty();
    }

    [Fact]
    public void Shapes_Add_ContainsNamedShape()
    {
        var frame = new TestFrame();
        var shape = new AnimationAARectFrame { Name = "Hitbox" };

        frame.Shapes.Add(shape);

        frame.Shapes.ShouldHaveSingleItem();
        frame.Shapes[0].Name.ShouldBe("Hitbox");
    }
}
