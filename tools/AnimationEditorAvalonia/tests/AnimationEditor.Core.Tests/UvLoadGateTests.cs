using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class UvLoadGateTests
{
    [Fact]
    public void DecideOutcome_PixelFile_MissingTextures_NotConfirmed_ReturnsLoadAsIs()
    {
        // Pixel files bypass the gate entirely — texture state and user answer don't matter.
        Assert.Equal(UvLoadOutcome.LoadAsIs,
            UvLoadGate.DecideOutcome(TextureCoordinateType.Pixel, allTexturesResolvable: false, userConfirmed: false));
    }

    [Fact]
    public void DecideOutcome_UvFile_MissingTextures_ReturnsRefuseMissingTextures()
    {
        Assert.Equal(UvLoadOutcome.RefuseMissingTextures,
            UvLoadGate.DecideOutcome(TextureCoordinateType.UV, allTexturesResolvable: false, userConfirmed: false));
    }

    [Fact]
    public void DecideOutcome_UvFile_AllPresent_UserDeclined_ReturnsRefuseUserDeclined()
    {
        Assert.Equal(UvLoadOutcome.RefuseUserDeclined,
            UvLoadGate.DecideOutcome(TextureCoordinateType.UV, allTexturesResolvable: true, userConfirmed: false));
    }

    [Fact]
    public void DecideOutcome_UvFile_AllPresent_UserConfirmed_ReturnsConvertAndLoad()
    {
        Assert.Equal(UvLoadOutcome.ConvertAndLoad,
            UvLoadGate.DecideOutcome(TextureCoordinateType.UV, allTexturesResolvable: true, userConfirmed: true));
    }

    [Fact]
    public void DecideOutcome_UvFile_MissingTextures_UserConfirmed_StillRefusesMissingTextures()
    {
        // User confirmation doesn't override a missing-texture refusal.
        Assert.Equal(UvLoadOutcome.RefuseMissingTextures,
            UvLoadGate.DecideOutcome(TextureCoordinateType.UV, allTexturesResolvable: false, userConfirmed: true));
    }
}
