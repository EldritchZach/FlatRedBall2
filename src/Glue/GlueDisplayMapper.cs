using System.Collections.Generic;
using GlueModel = FlatRedBall2.Glue.Model;
using Frb = FlatRedBall2.Rendering;

namespace FlatRedBall2.Glue;

/// <summary>
/// Translates Glue's project-level display block onto FRB2's <see cref="Frb.DisplaySettings"/>.
/// </summary>
/// <remarks>
/// A translation layer, not new camera work — the two types line up closely, and two of the enums
/// share both member names and ordinals. What does not line up is recorded as a diagnostic rather
/// than approximated silently.
/// </remarks>
public static class GlueDisplayMapper
{
    /// <summary>Applies <paramref name="source"/> to <paramref name="target"/>.</summary>
    /// <remarks>
    /// Project-global by nature, so apply it once at load rather than per screen. FRB2 applies
    /// window properties only on <c>Start</c>, which matches.
    /// </remarks>
    public static void Apply(
        GlueModel.DisplaySettings source, Frb.DisplaySettings target, List<GlueLoadDiagnostic> diagnostics)
    {
        // The author has opted out of generated display setup and configures the camera by hand.
        if (!source.GenerateDisplayCode)
            return;

        if (source.ResolutionWidth > 0)
            target.ResolutionWidth = source.ResolutionWidth;

        if (source.ResolutionHeight > 0)
            target.ResolutionHeight = source.ResolutionHeight;

        if (source.Scale > 0)
        {
            target.PreferredWindowWidth = target.ResolutionWidth * source.Scale / 100;
            target.PreferredWindowHeight = target.ResolutionHeight * source.Scale / 100;
        }

        // Same member names and same ordinals on both sides, so the cast is the mapping.
        target.ResizeMode = (Frb.ResizeMode)source.ResizeBehavior;
        target.DominantAxis = (Frb.DominantAxis)source.DominantInternalCoordinates;

        target.WindowMode = source.RunInFullScreen
            ? Frb.WindowMode.FullscreenBorderless
            : Frb.WindowMode.Windowed;

        target.AllowUserResizing = source.AllowWindowResizing;

        ApplyAspect(source, target, diagnostics);

        if (!source.Is2D)
        {
            Warn(diagnostics, "This project is not authored as 2D. FRB2 has no perspective camera, " +
                              "so it was loaded as 2D anyway.");
        }

        // XNA's TextureFilter enum: 0 is Linear, 1 is Point. Those are the only two FRB2 supports;
        // anything else (Anisotropic, the filtered-mip modes) is left unmapped and reported.
        switch (source.TextureFilter)
        {
            case 0:
                target.TextureFilterMode = Frb.TextureFilterMode.Linear;
                break;
            case 1:
                target.TextureFilterMode = Frb.TextureFilterMode.Point;
                break;
            default:
                Warn(diagnostics, $"Texture filter {source.TextureFilter} was authored, but FRB2 only " +
                                  "supports Point and Linear sampling.");
                break;
        }

        if (source.ScaleGum != 100)
        {
            Warn(diagnostics, $"A Gum scale of {source.ScaleGum}% was authored. FRB2 sizes its UI " +
                              "roots from the camera's own extents and has no independent Gum scale.");
        }
    }

    private static void ApplyAspect(
        GlueModel.DisplaySettings source, Frb.DisplaySettings target, List<GlueLoadDiagnostic> diagnostics)
    {
        const int NoAspectRatio = 0;
        const int RangedAspectRatio = 2;

        // The ratio members are meaningless unless the behaviour asks for them, and three real
        // projects carry a stale ratio alongside NoAspectRatio. Reading it unconditionally would
        // letterbox games that should fill the window.
        if (source.AspectRatioBehavior == NoAspectRatio)
        {
            target.AspectPolicy = Frb.AspectPolicy.Free;
            target.FixedAspectRatio = null;
            return;
        }

        target.AspectPolicy = Frb.AspectPolicy.Locked;

        if (source.AspectRatioHeight != 0)
            target.FixedAspectRatio = (float)(source.AspectRatioWidth / source.AspectRatioHeight);

        if (source.AspectRatioBehavior == RangedAspectRatio)
        {
            Warn(diagnostics, "A ranged aspect ratio was authored. FRB2 locks or frees the aspect " +
                              "with no band between, so the first ratio was used as a fixed lock.");
        }
    }

    private static void Warn(List<GlueLoadDiagnostic> diagnostics, string message) =>
        diagnostics.Add(new GlueLoadDiagnostic(GlueDiagnosticSeverity.Warning, message, null));
}
