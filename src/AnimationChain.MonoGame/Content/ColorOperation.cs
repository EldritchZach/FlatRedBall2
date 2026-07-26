using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// How a frame's per-frame color (<c>AnimationFrame.Red</c>/<c>AnimationFrame.Green</c>/
/// <c>AnimationFrame.Blue</c>) combines with the sprite's texture. A <c>null</c> operation
/// means none. Authored in the Animation Editor and stored in the .achx.
/// </summary>
public enum ColorOperation
{
    /// <summary>
    /// Multiply the texture by the color (darken / colorize). White is the identity. Applied
    /// automatically by <see cref="SpriteBatchExtensions.DrawAnimation"/>.
    /// </summary>
    Multiply,

    /// <summary>
    /// Add the color to the texture (brighten / glow / flash). Black is the identity. Not yet
    /// applied at runtime — <see cref="SpriteBatch"/> can only multiply texture color, not offset
    /// it, so this needs a custom shader. Authored data only for now.
    /// </summary>
    Add,
}
