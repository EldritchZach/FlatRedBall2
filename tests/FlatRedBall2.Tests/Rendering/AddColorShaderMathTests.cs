using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

// We can't compile/run src/Shaders/AddColor.fx in a unit test (no GraphicsDevice - see
// SpriteRotationTests for the same constraint), so this replicates MainPS's formula in C# and
// pins the exact regression hit while building the manual demo: MonoGame premultiplies alpha
// into a texture's RGB on load, so a fully transparent texel already has rgb == 0. Adding
// ColorOffset unconditionally leaked a flat color into transparent pixels regardless of alpha,
// because SpriteBatch's premultiplied blend state (SourceBlend = One) adds shader output rgb
// directly, without re-scaling by alpha. The fix scales the offset by the texel's own alpha
// before adding it. Keep this in sync with MainPS if that formula changes.
public class AddColorShaderMathTests
{
    private static Vector3 MainPsRgb(Vector3 texRgb, float texAlpha, Vector3 colorOffset)
        => Vector3.Clamp(texRgb + colorOffset * texAlpha, Vector3.Zero, Vector3.One);

    [Fact]
    public void MainPsRgb_TransparentTexel_ContributesNoColor()
    {
        var result = MainPsRgb(Vector3.Zero, texAlpha: 0f, colorOffset: new Vector3(0.6f, 0.6f, 0f));

        result.ShouldBe(Vector3.Zero);
    }

    [Fact]
    public void MainPsRgb_OpaqueTexel_AddsFullOffset()
    {
        var result = MainPsRgb(new Vector3(0.1f, 0.1f, 0.1f), texAlpha: 1f, colorOffset: new Vector3(0.6f, 0.6f, 0f));

        result.X.ShouldBe(0.7f, tolerance: 0.0001f);
        result.Y.ShouldBe(0.7f, tolerance: 0.0001f);
        result.Z.ShouldBe(0.1f, tolerance: 0.0001f);
    }
}
