using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

public class AnimationChainTests
{
    private class TestFrame : AnimationFrameBase { }

    [Fact]
    public void Name_Default_IsEmpty()
    {
        var chain = new AnimationChain<TestFrame>();

        chain.Name.ShouldBe(string.Empty);
    }

    [Fact]
    public void TotalLength_SumsFrameLengths()
    {
        var chain = new AnimationChain<TestFrame>
        {
            new TestFrame { FrameLength = TimeSpan.FromSeconds(0.1) },
            new TestFrame { FrameLength = TimeSpan.FromSeconds(0.25) },
        };

        chain.TotalLength.ShouldBe(TimeSpan.FromSeconds(0.35));
    }
}
