using System.Collections.Generic;

namespace FlatRedBall2.Glue.Model;

/// <summary>
/// An asset a Glue project or element depends on. Phase 1 retains these without touching disk;
/// loading and content-manager semantics are Phase 4.
/// </summary>
public class ReferencedFileSave
{
    /// <summary>Project-relative path to the asset, as authored.</summary>
    public string? Name { get; set; }

    /// <summary>The runtime type the asset loads as, when Glue records one.</summary>
    public string? RuntimeType { get; set; }

    /// <summary>Glue's name/value bag for this file.</summary>
    public List<PropertySave> Properties { get; set; } = new();

    /// <summary>Whether one shared instance is used rather than a per-element copy.</summary>
    public bool IsSharedStatic { get; set; }

    /// <summary>Whether the asset is unloaded when its owner is destroyed.</summary>
    public bool DestroyOnUnload { get; set; }

    /// <summary>Whether the asset is exposed as a public member on its owner.</summary>
    public bool HasPublicProperty { get; set; }

    /// <summary>Whether the asset is loaded at runtime rather than at build time.</summary>
    public bool LoadedAtRuntime { get; set; }

    /// <summary>Whether Glue created this entry from a wildcard pattern rather than explicitly.</summary>
    public bool IsCreatedByWildcard { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name ?? base.ToString()!;
}
