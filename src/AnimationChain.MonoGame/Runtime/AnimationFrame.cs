using FlatRedBall2.AnimationEditorCommon;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// One frame of a texture-based MonoGame/KNI animation. Closes <see cref="AnimationFrameBase{TTexture}"/>
/// over <see cref="Texture2D"/> — timing, flip flags, per-frame offsets, per-frame color, and shapes
/// are all inherited; see that base type for their docs. <see cref="AnimationFrameBase.SourceRectangle"/>
/// is a renderer-agnostic <see cref="PixelRectangle"/>; convert it with
/// <see cref="PixelRectangleExtensions.ToXnaRectangle"/> before passing it to <see cref="SpriteBatch"/>.
/// </summary>
public class AnimationFrame : AnimationFrameBase<Texture2D>
{
}
