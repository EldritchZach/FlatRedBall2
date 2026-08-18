using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Structural layout for the "add frame" cursor (#882) -- an arrow with a "+" badge, replacing
// the OS crosshair. Pure data only; the SkiaSharp rasterization is an untested adapter in
// WireframeControl.CreateAddFrameCursor.

public class AddFrameCursorShapeTests
{
    [Fact]
    public void BadgeRegion_ArrowRegion_DoNotOverlap()
    {
        Assert.False(AddFrameCursorShape.ArrowRegion.Overlaps(AddFrameCursorShape.BadgeRegion));
    }

    [Fact]
    public void BadgeRegion_SitsInBottomRightQuadrant()
    {
        var badge = AddFrameCursorShape.BadgeRegion;
        int centerX = badge.X + badge.Width / 2;
        int centerY = badge.Y + badge.Height / 2;

        Assert.True(centerX > AddFrameCursorShape.Width / 2);
        Assert.True(centerY > AddFrameCursorShape.Height / 2);
    }

    [Fact]
    public void HotSpot_IsAtArrowTip_NotCentered()
    {
        // The arrow tip is the region's top-left corner -- matching the OS default arrow
        // cursor's hotspot convention, unlike the crosshair cursor this replaces (centered).
        Assert.Equal(AddFrameCursorShape.HotSpotX, AddFrameCursorShape.ArrowRegion.X);
        Assert.Equal(AddFrameCursorShape.HotSpotY, AddFrameCursorShape.ArrowRegion.Y);
        Assert.NotEqual(AddFrameCursorShape.Width / 2, AddFrameCursorShape.HotSpotX);
        Assert.NotEqual(AddFrameCursorShape.Height / 2, AddFrameCursorShape.HotSpotY);
    }
}
