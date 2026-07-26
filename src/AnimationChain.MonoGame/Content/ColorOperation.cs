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
    /// Add the color to the texture (brighten / glow / flash). Black is the identity. Applied
    /// automatically by <see cref="SpriteBatchExtensions.DrawAnimation"/> via a pixel shader, since
    /// <see cref="SpriteBatch"/> can only multiply texture color, not offset it.
    /// </summary>
    Add,
}
