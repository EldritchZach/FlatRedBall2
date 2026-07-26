using FlatRedBall2.Animation;
using SkiaSharp;

namespace AnimationEditor.App;

/// <summary>
/// Maps a frame's <see cref="ColorOperation"/> + per-channel R/G/B to the SkiaSharp <c>SKColorFilter</c>
/// the preview applies. This is the editor's <em>reference</em> interpretation of the operation — runtimes
/// that consume the same <c>.achx</c> render it however they choose (see the animation-editor skill).
/// </summary>
public static class FrameColorFilter
{
    /// <summary>
    /// Returns the <c>SKColorFilter</c> to assign to a paint, or <c>null</c> when <paramref name="operation"/>
    /// is <c>null</c> (no color effect). Unset channels default to each operation's identity — 255 for
    /// Multiply (×1), 0 for Add (+0) — so a partially-authored color only affects the channels the artist set.
    /// </summary>
    public static SKColorFilter? Create(ColorOperation? operation, int? r, int? g, int? b)
    {
        switch (operation)
        {
            case ColorOperation.Multiply:
                // Channels feed an SKColor byte, so a negative value must clamp to 0 here — an
                // unclamped negative int->byte cast wraps (-10 becomes 246) instead of darkening.
                return SKColorFilter.CreateBlendMode(
                    new SKColor((byte)Math.Clamp(r ?? 255, 0, 255), (byte)Math.Clamp(g ?? 255, 0, 255), (byte)Math.Clamp(b ?? 255, 0, 255), 255),
                    SKBlendMode.Modulate);
            case ColorOperation.Add:
                // A Plus blend can't do this: it blends in premultiplied space, so an alpha-0 color adds
                // nothing, and an alpha-255 color would also add to alpha and force the sprite opaque.
                // A color matrix adds the channel offsets to RGB and leaves the alpha row as identity.
                // SkiaSharp's matrix works in normalized [0,1] space, so offsets are channel/255.
                return SKColorFilter.CreateColorMatrix(new[]
                {
                    1f, 0f, 0f, 0f, (r ?? 0) / 255f,
                    0f, 1f, 0f, 0f, (g ?? 0) / 255f,
                    0f, 0f, 1f, 0f, (b ?? 0) / 255f,
                    0f, 0f, 0f, 1f, 0f,
                });
            default:
                return null;
        }
    }
}
