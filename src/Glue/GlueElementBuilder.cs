using System;
using System.Collections.Generic;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Builds the whole set of objects an element declares. Shared by <see cref="GlueScreen"/> and
/// <see cref="GlueEntity"/>, which differ only in how a single object gets registered.
/// </summary>
internal static class GlueElementBuilder
{
    /// <remarks>
    /// <c>addSingle</c> is how one object gets registered on its owner — a screen adds renderables,
    /// an entity attaches children. Lists have no owner to register with, so their members are built
    /// but not added.
    /// </remarks>
    internal static void Build(
        List<NamedObjectSave> namedObjects,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics,
        Func<GlueObjectBuilder, NamedObjectSave, object?> addSingle)
    {
        var builder = new GlueObjectBuilder(diagnostics);

        foreach (var save in namedObjects)
        {
            if (string.IsNullOrEmpty(save.InstanceName))
                continue;

            object? built = save.IsList
                ? BuildList(builder, save, elementName, diagnostics)
                : addSingle(builder, save);

            if (built is not null)
                objects[save.InstanceName] = built;
        }
    }

    /// <summary>
    /// Builds a list member's contents. The list itself is a plain <see cref="List{T}"/> of whatever
    /// could be constructed — FRB2 has no equivalent of Glue's <c>PositionedObjectList&lt;T&gt;</c>,
    /// and this phase does not need one.
    /// </summary>
    /// <remarks>
    /// Members are built but not registered anywhere: a list in Glue is usually a spawn pool whose
    /// contents an entity factory owns, which is Phase 8. Anything unbuildable — most commonly a
    /// nested entity, which needs Phase 6 — is reported and skipped.
    /// </remarks>
    private static object BuildList(
        GlueObjectBuilder builder,
        NamedObjectSave save,
        string? elementName,
        List<GlueLoadDiagnostic> diagnostics)
    {
        var items = new List<object>();

        foreach (var contained in save.ContainedObjects)
        {
            object? item = builder.Create(contained, elementName);
            if (item is not null)
                items.Add(item);
        }

        return items;
    }
}
