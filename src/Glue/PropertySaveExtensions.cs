using System;
using System.Collections.Generic;
using System.Text.Json;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Reads typed values out of a Glue name/value bag. Mirrors FRB1's
/// <c>PropertySaveListExtensions.GetValue&lt;T&gt;</c>, including its tolerance: a missing name or an
/// undecodable value yields <c>default(T)</c> rather than throwing, because a partially readable
/// project is more useful than none.
/// </summary>
public static class PropertySaveExtensions
{
    /// <summary>
    /// Finds <paramref name="name"/> and decodes its value as <typeparamref name="T"/>. The
    /// requested type is authoritative — <see cref="PropertySave.Type"/> is never consulted, since
    /// Glue omits it on some entries and it can disagree with the stored value.
    /// </summary>
    /// <returns>The decoded value, or <c>default(T)</c> if absent or not decodable as this type.</returns>
    public static T? GetValue<T>(this IReadOnlyList<PropertySave>? properties, string name)
    {
        if (properties is null)
            return default;

        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].Name == name)
                return Decode<T>(properties[i].Value);
        }

        return default;
    }

    /// <summary>Whether an entry with this name exists, regardless of whether its value decodes.</summary>
    public static bool ContainsValue(this IReadOnlyList<PropertySave>? properties, string name)
    {
        if (properties is null)
            return false;

        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].Name == name)
                return true;
        }

        return false;
    }

    private static T? Decode<T>(JsonElement value)
    {
        // Unwrap int?/float?/etc. so one code path serves both the nullable and non-nullable request.
        Type target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        if (target.IsEnum)
        {
            // Glue writes enums as bare ints with no string converter.
            return value.TryGetInt32(out int enumValue)
                ? (T)Enum.ToObject(target, enumValue)
                : default;
        }

        if (target == typeof(string))
            return value.ValueKind == JsonValueKind.String ? (T)(object)value.GetString()! : default;

        if (target == typeof(bool))
        {
            return value.ValueKind switch
            {
                JsonValueKind.True => (T)(object)true,
                JsonValueKind.False => (T)(object)false,
                _ => default,
            };
        }

        if (target == typeof(int))
            return value.TryGetInt32(out int i) ? (T)(object)i : default;

        if (target == typeof(long))
            return value.TryGetInt64(out long l) ? (T)(object)l : default;

        if (target == typeof(float))
            return value.TryGetSingle(out float f) ? (T)(object)f : default;

        if (target == typeof(double))
            return value.TryGetDouble(out double d) ? (T)(object)d : default;

        if (target == typeof(decimal))
            return value.TryGetDecimal(out decimal m) ? (T)(object)m : default;

        return default;
    }
}
