using AnimationEditor.Core.Layout;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="PreviewPaneHeightValidator"/> — the safeguard that keeps a corrupt or
/// stale settings value from producing a broken window layout (issue #904).
/// </summary>
public class PreviewPaneHeightValidatorTests
{
    [Fact]
    public void Resolve_StoredWithinBounds_ReturnsStoredValue()
    {
        var resolved = PreviewPaneHeightValidator.Resolve(320.0, min: 80, max: 2000, fallback: 250);

        Assert.Equal(320.0, resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(5000.0)]
    public void Resolve_InvalidStoredValue_ReturnsFallback(double? stored)
    {
        var resolved = PreviewPaneHeightValidator.Resolve(stored, min: 80, max: 2000, fallback: 250);

        Assert.Equal(250.0, resolved);
    }
}
