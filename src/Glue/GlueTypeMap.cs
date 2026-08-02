using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace FlatRedBall2.Glue;

/// <summary>
/// Maps Glue's type strings onto FRB2 types.
/// </summary>
/// <remarks>
/// Deliberately small: it covers only the types this phase can build. Everything else — tile
/// collision, collision relationships, camera controllers, lists — is owned by a later phase and
/// reports as unmapped until that phase lands. An unmapped type is a diagnostic, never a failure.
/// </remarks>
public static class GlueTypeMap
{
    // Keyed on the open type name so a generic's arguments do not have to match for the outer type
    // to resolve. Adding a row is how a later phase claims a type.
    private static readonly Dictionary<string, Type> TypesByGlueName = new(StringComparer.Ordinal)
    {
        ["FlatRedBall.Sprite"] = typeof(Rendering.Sprite),
        ["FlatRedBall.Math.Geometry.AxisAlignedRectangle"] = typeof(Collision.AARect),
        ["FlatRedBall.Math.Geometry.Circle"] = typeof(Collision.Circle),
        ["FlatRedBall.Math.Geometry.Polygon"] = typeof(Collision.Polygon),
    };

    /// <summary>Resolves a parsed Glue type name to its FRB2 equivalent.</summary>
    /// <returns>True if this build can construct the type; false if a later phase owns it.</returns>
    public static bool TryGetType(GlueTypeName typeName, [NotNullWhen(true)] out Type? type)
    {
        type = null;

        // An element reference names a Screen or Entity in the project, not a CLR type.
        if (typeName.IsElementReference)
            return false;

        return TypesByGlueName.TryGetValue(typeName.OpenTypeName, out type);
    }

    /// <summary>Resolves a raw Glue type string, parsing it first.</summary>
    public static bool TryGetType(string? glueTypeString, [NotNullWhen(true)] out Type? type) =>
        TryGetType(GlueTypeName.Parse(glueTypeString), out type);
}
