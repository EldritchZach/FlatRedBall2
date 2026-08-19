using AnimationEditor.Core.IO;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class AnimationCloneHelperTests
{
    [Fact]
    public void CloneFrame_AllFieldsSetToNonDefaultValues_CopiesEveryField()
    {
        var source = new AnimationFrameSave
        {
            TextureName = "walk.png",
            LeftCoordinate = 0.1f,
            RightCoordinate = 0.9f,
            TopCoordinate = 0.2f,
            BottomCoordinate = 0.8f,
            FrameLength = 0.5f,
            FlipHorizontal = true,
            FlipVertical = true,
            FlipDiagonal = true,
            RelativeX = 3f,
            RelativeY = -4f,
            Red = 10,
            Green = 20,
            Blue = 30,
            Alpha = 40,
            ColorOperation = ColorOperation.Add,
        };

        var copy = AnimationCloneHelper.CloneFrame(source);

        Assert.Equal(source.TextureName, copy.TextureName);
        Assert.Equal(source.LeftCoordinate, copy.LeftCoordinate);
        Assert.Equal(source.RightCoordinate, copy.RightCoordinate);
        Assert.Equal(source.TopCoordinate, copy.TopCoordinate);
        Assert.Equal(source.BottomCoordinate, copy.BottomCoordinate);
        Assert.Equal(source.FrameLength, copy.FrameLength);
        Assert.Equal(source.FlipHorizontal, copy.FlipHorizontal);
        Assert.Equal(source.FlipVertical, copy.FlipVertical);
        Assert.Equal(source.FlipDiagonal, copy.FlipDiagonal);
        Assert.Equal(source.RelativeX, copy.RelativeX);
        Assert.Equal(source.RelativeY, copy.RelativeY);
        Assert.Equal(source.Red, copy.Red);
        Assert.Equal(source.Green, copy.Green);
        Assert.Equal(source.Blue, copy.Blue);
        Assert.Equal(source.Alpha, copy.Alpha);
        Assert.Equal(source.ColorOperation, copy.ColorOperation);
    }
}
