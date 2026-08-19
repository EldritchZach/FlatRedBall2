namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// A renderer-agnostic pixel-coordinate rectangle — the source-region type used by
/// <see cref="AnimationFrameBase"/> so this project has no MonoGame/KNI dependency. Renderers
/// convert this to their own rectangle type (e.g. <c>Microsoft.Xna.Framework.Rectangle</c>) when
/// drawing a frame.
/// </summary>
public struct PixelRectangle
{
    /// <summary>Left edge, in pixels.</summary>
    public int X;

    /// <summary>Top edge, in pixels.</summary>
    public int Y;

    /// <summary>Width, in pixels.</summary>
    public int Width;

    /// <summary>Height, in pixels.</summary>
    public int Height;

    /// <summary>Creates a rectangle from its left/top/width/height, all in pixels.</summary>
    public PixelRectangle(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}
