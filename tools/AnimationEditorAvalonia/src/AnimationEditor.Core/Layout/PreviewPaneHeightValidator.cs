namespace AnimationEditor.Core.Layout;

/// <summary>
/// Resolves the persisted preview-pane row height (issue #904) against sane bounds so a
/// missing, corrupt, or otherwise nonsensical stored value can never produce a broken window
/// layout — a valid-JSON-but-bad-number case the whole-file settings load's try/catch doesn't
/// catch on its own.
/// </summary>
public static class PreviewPaneHeightValidator
{
    public static double Resolve(double? stored, double min, double max, double fallback)
    {
        if (stored is not { } value)
            return fallback;
        if (double.IsNaN(value) || double.IsInfinity(value))
            return fallback;
        if (value < min || value > max)
            return fallback;
        return value;
    }
}
