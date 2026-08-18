namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Axis-aligned integer pixel rectangle. Used by <see cref="AddFrameCursorShape"/> to describe
/// cursor-bitmap regions without a SkiaSharp/Avalonia dependency in Core.
/// </summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    /// <summary>True if this rect and <paramref name="other"/> share any interior pixels.
    /// Edges that merely touch (e.g. this.Right == other.X) do not count as overlapping.</summary>
    public bool Overlaps(PixelRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
}

/// <summary>
/// Pure structural layout for the "add frame" cursor (issue #882): a normal arrow with a small
/// "+" badge at its bottom-right -- the conventional "add"/"create new" affordance (e.g. Windows
/// Explorer's drag-copy cursor), replacing the OS crosshair (<c>StandardCursorType.Cross</c>)
/// previously shown while Ctrl is held over the wireframe. Exposed as plain data -- bitmap size,
/// hotspot, and two non-overlapping named regions -- so the layout is unit-testable without
/// SkiaSharp/Avalonia. <c>WireframeControl.CreateAddFrameCursor</c> is the untested adapter that
/// rasterizes these regions via Skia and wraps the result as an Avalonia <c>Cursor</c>.
/// </summary>
public static class AddFrameCursorShape
{
    public const int Width = 24;
    public const int Height = 24;

    /// <summary>Hotspot at the arrow's tip (top-left), matching the OS default arrow cursor's
    /// hotspot convention -- unlike the crosshair cursor this replaces, whose hotspot is centered.</summary>
    public const int HotSpotX = 0;
    public const int HotSpotY = 0;

    /// <summary>Bounding box of the arrow glyph, anchored at the hotspot.</summary>
    public static readonly PixelRect ArrowRegion = new(0, 0, 13, 13);

    /// <summary>Bounding box of the "+" badge -- clear of the arrow, in the bottom-right quadrant.</summary>
    public static readonly PixelRect BadgeRegion = new(13, 13, 11, 11);
}
