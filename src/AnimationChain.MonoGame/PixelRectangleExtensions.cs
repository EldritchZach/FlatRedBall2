using FlatRedBall2.AnimationEditorCommon;
using Microsoft.Xna.Framework;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// Converts <c>AnimationEditorCommon</c>'s renderer-agnostic <see cref="PixelRectangle"/>
/// to MonoGame/KNI's <see cref="Rectangle"/> for use with <see cref="Microsoft.Xna.Framework.Graphics.SpriteBatch"/>.
/// </summary>
public static class PixelRectangleExtensions
{
    /// <summary>Converts <paramref name="rect"/>'s X/Y/Width/Height fields directly into a <see cref="Rectangle"/>.</summary>
    public static Rectangle ToXnaRectangle(this PixelRectangle rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
