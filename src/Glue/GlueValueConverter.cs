using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace FlatRedBall2.Glue;

/// <summary>
/// Converts a raw Glue JSON value into a target CLR type. Glue stores values untyped, so the target
/// property's declared type is what drives the conversion.
/// </summary>
internal static class GlueValueConverter
{
    private static readonly Dictionary<string, XnaColor> NamedColors = BuildNamedColors();

    /// <summary>Attempts to convert <paramref name="value"/> to <paramref name="targetType"/>.</summary>
    /// <returns>False if the value cannot represent that type; the caller reports it.</returns>
    public static bool TryConvert(JsonElement value, Type targetType, out object? converted)
    {
        converted = null;
        Type target = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (target == typeof(string))
        {
            if (value.ValueKind != JsonValueKind.String)
                return false;

            converted = value.GetString();
            return true;
        }

        if (target == typeof(bool))
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.True: converted = true; return true;
                case JsonValueKind.False: converted = false; return true;
                default: return false;
            }
        }

        if (target == typeof(XnaColor))
            return TryConvertColor(value, out converted);

        if (target.IsEnum)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int enumValue))
            {
                converted = Enum.ToObject(target, enumValue);
                return true;
            }

            // Some Glue values arrive as the member's name rather than its number.
            if (value.ValueKind == JsonValueKind.String)
                return Enum.TryParse(target, value.GetString(), ignoreCase: true, out converted);

            return false;
        }

        if (value.ValueKind != JsonValueKind.Number)
            return false;

        if (target == typeof(float) && value.TryGetSingle(out float f)) { converted = f; return true; }
        if (target == typeof(double) && value.TryGetDouble(out double d)) { converted = d; return true; }
        if (target == typeof(int) && value.TryGetInt32(out int i)) { converted = i; return true; }
        if (target == typeof(long) && value.TryGetInt64(out long l)) { converted = l; return true; }
        if (target == typeof(decimal) && value.TryGetDecimal(out decimal m)) { converted = m; return true; }
        if (target == typeof(byte) && value.TryGetByte(out byte b)) { converted = b; return true; }

        return false;
    }

    /// <summary>
    /// Glue writes colours as a name (<c>"White"</c>), and occasionally as a packed integer. FRB2 has
    /// no named-colour lookup of its own, so this reads XNA's own static colour properties.
    /// </summary>
    private static bool TryConvertColor(JsonElement value, out object? converted)
    {
        converted = null;

        if (value.ValueKind == JsonValueKind.String)
        {
            string? name = value.GetString();
            if (name is not null && NamedColors.TryGetValue(name, out var namedColor))
            {
                converted = namedColor;
                return true;
            }

            // "#RRGGBB" / "#RRGGBBAA"
            if (name is not null && name.StartsWith('#') && TryParseHexColor(name, out var hexColor))
            {
                converted = hexColor;
                return true;
            }

            return false;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out uint packed))
        {
            converted = new XnaColor(
                (byte)(packed & 0xFF),
                (byte)((packed >> 8) & 0xFF),
                (byte)((packed >> 16) & 0xFF),
                (byte)((packed >> 24) & 0xFF));
            return true;
        }

        return false;
    }

    private static bool TryParseHexColor(string text, out XnaColor color)
    {
        color = default;
        ReadOnlySpan<char> digits = text.AsSpan(1);

        if (digits.Length is not (6 or 8))
            return false;

        if (!uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint raw))
            return false;

        if (digits.Length == 6)
        {
            color = new XnaColor((byte)(raw >> 16), (byte)(raw >> 8), (byte)raw);
            return true;
        }

        color = new XnaColor((byte)(raw >> 24), (byte)(raw >> 16), (byte)(raw >> 8), (byte)raw);
        return true;
    }

    /// <summary>
    /// The colour names Glue can author, as data rather than reflection.
    /// </summary>
    /// <remarks>
    /// Reading these off <c>Color</c>'s static properties would be shorter, but reflection over a
    /// type in another assembly is not trim-safe, and rooting it by attribute fails too: the
    /// backing assembly is <c>MonoGame.Framework</c> on desktop and an <c>nkast.Xna.Framework</c>
    /// assembly on the web target, so no single <c>DynamicDependency</c> resolves on both. Under
    /// trimming the table would come back empty and every named colour would silently fall back to
    /// the engine default — a failure visible only after publish.
    /// <para>A name outside this list converts to nothing and warns, which is loud and fixable.
    /// Extend the list rather than reaching back for reflection.</para>
    /// </remarks>
    private static Dictionary<string, XnaColor> BuildNamedColors() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["White"] = XnaColor.White,
            ["Black"] = XnaColor.Black,
            ["Transparent"] = XnaColor.Transparent,
            ["TransparentBlack"] = XnaColor.Transparent,
            ["Red"] = XnaColor.Red,
            ["Green"] = XnaColor.Green,
            ["Blue"] = XnaColor.Blue,
            ["Yellow"] = XnaColor.Yellow,
            ["Cyan"] = XnaColor.Cyan,
            ["Magenta"] = XnaColor.Magenta,
            ["Orange"] = XnaColor.Orange,
            ["Purple"] = XnaColor.Purple,
            ["Pink"] = XnaColor.Pink,
            ["Brown"] = XnaColor.Brown,
            ["Gray"] = XnaColor.Gray,
            ["Grey"] = XnaColor.Gray,
            ["LightGray"] = XnaColor.LightGray,
            ["DarkGray"] = XnaColor.DarkGray,
            ["Lime"] = XnaColor.Lime,
            ["Teal"] = XnaColor.Teal,
            ["Navy"] = XnaColor.Navy,
            ["Olive"] = XnaColor.Olive,
            ["Maroon"] = XnaColor.Maroon,
            ["Silver"] = XnaColor.Silver,
            ["Gold"] = XnaColor.Gold,
            ["CornflowerBlue"] = XnaColor.CornflowerBlue,
        };
}
