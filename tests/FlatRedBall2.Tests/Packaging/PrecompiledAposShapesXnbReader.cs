using System;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Tests.Packaging;

/// <summary>
/// Reads MonoGame MGFX / assembly metadata embedded in a precompiled <c>apos-shapes.xnb</c>.
/// LZ4-compressed XNBs still expose the EffectReader assembly tag and MGFX header as
/// plaintext in the compressed blob — no full decompress needed.
/// </summary>
internal static class PrecompiledAposShapesXnbReader
{
    private static readonly Regex EffectReaderVersionRegex = new(
        @"EffectReader.{0,80}Version=([\d.]+)",
        RegexOptions.CultureInvariant);

    public static int GetMaxMgfxVersionAcceptedByRuntime()
    {
        var headerType = typeof(Effect).GetNestedType("MGFXHeader", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MGFXHeader not found on Effect.");
        return (int)headerType.GetField(
            "MGFXVersion",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.GetValue(null)!;
    }

    public static string GetMonoGameAssemblyVersion(byte[] xnbBytes)
    {
        var text = Encoding.Latin1.GetString(xnbBytes);
        var match = EffectReaderVersionRegex.Match(text);
        if (!match.Success)
        {
            throw new InvalidOperationException(
                "apos-shapes.xnb is missing a MonoGame EffectReader assembly tag.");
        }

        return match.Groups[1].Value;
    }

    public static int GetMgfxVersion(byte[] xnbBytes)
    {
        var signature = new byte[] { (byte)'M', (byte)'G', (byte)'F', (byte)'X' };
        for (var i = 0; i <= xnbBytes.Length - 8; i++)
        {
            if (xnbBytes[i] != signature[0] || xnbBytes[i + 1] != signature[1]
                || xnbBytes[i + 2] != signature[2] || xnbBytes[i + 3] != signature[3])
            {
                continue;
            }

            var version = xnbBytes[i + 4];
            if (version is > 0 and < 32)
            {
                return version;
            }
        }

        throw new InvalidOperationException("apos-shapes.xnb does not contain a readable MGFX version header.");
    }
}
