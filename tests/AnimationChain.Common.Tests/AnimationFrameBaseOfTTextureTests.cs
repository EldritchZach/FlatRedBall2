using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

public class AnimationFrameBaseOfTTextureTests
{
    // Stand-in texture — proves the generic closes over any type, not just a MonoGame Texture2D.
    private class FakeTexture { }

    private class TestFrame : AnimationFrameBase<FakeTexture> { }

    [Fact]
    public void Texture_Assign_RoundTrips()
    {
        var frame = new TestFrame();
        var texture = new FakeTexture();

        frame.Texture = texture;

        frame.Texture.ShouldBeSameAs(texture);
    }

    [Fact]
    public void Texture_Default_IsNull()
    {
        var frame = new TestFrame();

        frame.Texture.ShouldBeNull();
    }
}
