namespace FlatRedBall2.Glue.Model;

/// <summary>The contents of one Glue <c>.glsj</c> file.</summary>
public class ScreenSave : GlueElement
{
    /// <summary>
    /// The screen this one derives from, in the same backslash form as <see cref="GlueElement.Name"/>.
    /// Phase 1 retains it without merging; resolution is Phase 6.
    /// </summary>
    public string? BaseScreen { get; set; }

    /// <summary>The screen to advance to when this one finishes.</summary>
    public string? NextScreen { get; set; }
}
