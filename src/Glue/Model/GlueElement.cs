using System.Collections.Generic;

namespace FlatRedBall2.Glue.Model;

/// <summary>
/// Shared shape of a Glue Screen and Entity — the contents of one <c>.glsj</c> or <c>.glej</c> file.
/// </summary>
public abstract class GlueElement
{
    /// <summary>
    /// The element's identity, project-relative and backslash-separated with no extension
    /// (<c>Screens\Level1</c>). Normalize separators when building a path from it, but compare it
    /// as-is: this same form is what <c>StartUpScreen</c> and <c>BaseScreen</c> reference.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Objects declared in this element.</summary>
    public List<NamedObjectSave> NamedObjects { get; set; } = new();

    /// <summary>Variables exposed on this element. Applied in Phase 3.</summary>
    public List<CustomVariable> CustomVariables { get; set; } = new();

    /// <summary>Assets this element needs. Loaded in Phase 4.</summary>
    public List<ReferencedFileSave> ReferencedFiles { get; set; } = new();

    /// <summary>Uncategorized states. Applied in Phase 7.</summary>
    public List<StateSave> States { get; set; } = new();

    /// <summary>Categorized states. Applied in Phase 7.</summary>
    public List<StateSaveCategory> StateCategoryList { get; set; } = new();

    /// <summary>Glue's name/value bag for this element.</summary>
    public List<PropertySave> Properties { get; set; } = new();

    /// <summary>Whether this element's content is loaded into the global content manager.</summary>
    public bool UseGlobalContent { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name ?? base.ToString()!;
}
