using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// Applies an <see cref="AnimationFrame"/>'s authored color channels to a base draw
/// <see cref="Color"/>. Used by <see cref="SpriteBatchExtensions.DrawAnimation"/>.
/// </summary>
public static class AnimationFrameColor
{
    /// <summary>
    /// Combines <paramref name="frame"/>'s color channels with <paramref name="baseColor"/> and
    /// returns the resulting <see cref="Color"/> to pass to <see cref="SpriteBatch"/>'s draw call.
    /// <para>
    /// <see cref="ColorOperation.Multiply"/> scales R/G/B by <see cref="AnimationFrame.Red"/>/
    /// <see cref="AnimationFrame.Green"/>/<see cref="AnimationFrame.Blue"/> (unset channels default
    /// to 255, the identity). <see cref="ColorOperation.Add"/> is not yet applied at runtime — it
    /// needs a custom shader, since <see cref="SpriteBatch"/> can only multiply, not offset, texture
    /// color — so RGB passes through unchanged when a frame's operation is <c>Add</c> or <c>null</c>.
    /// </para>
    /// <para>
    /// <see cref="AnimationFrame.Alpha"/> is independent of <see cref="AnimationFrame.ColorOperation"/>
    /// and always scales the base color's alpha when set.
    /// </para>
    /// </summary>
    public static Color Apply(AnimationFrame frame, Color baseColor)
    {
        var result = baseColor;

        if (frame.ColorOperation == ColorOperation.Multiply)
        {
            int r = System.Math.Clamp(frame.Red ?? 255, 0, 255);
            int g = System.Math.Clamp(frame.Green ?? 255, 0, 255);
            int b = System.Math.Clamp(frame.Blue ?? 255, 0, 255);
            result.R = (byte)(result.R * r / 255);
            result.G = (byte)(result.G * g / 255);
            result.B = (byte)(result.B * b / 255);
        }

        if (frame.Alpha.HasValue)
        {
            int a = System.Math.Clamp(frame.Alpha.Value, 0, 255);
            result.A = (byte)(result.A * a / 255);
        }

        return result;
    }
}
