using FlatRedBall2;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering.Batches;

/// <summary>
/// Same screen-space setup as <see cref="ScreenSpaceBatch"/>, but draws through the add-color
/// pixel shader (<see cref="AddColorEffect"/>) instead of the default <c>SpriteEffect</c>. See
/// <see cref="WorldSpaceAddColorBatch"/> for why <see cref="SpriteSortMode.Immediate"/> is required.
/// </summary>
public class ScreenSpaceAddColorBatch : IRenderBatch
{
    /// <summary>The shared singleton instance.</summary>
    public static readonly ScreenSpaceAddColorBatch Instance = new ScreenSpaceAddColorBatch();

    /// <inheritdoc/>
    public bool FlipsY => false;

    /// <inheritdoc/>
    public void Begin(SpriteBatch spriteBatch, Camera camera)
        => spriteBatch.Begin(
            sortMode: SpriteSortMode.Immediate,
            samplerState: FlatRedBallService.Default.DisplaySettings.GetSamplerState(),
            effect: AddColorEffect.Get(spriteBatch.GraphicsDevice));

    /// <inheritdoc/>
    public void End(SpriteBatch spriteBatch) => spriteBatch.End();
}
