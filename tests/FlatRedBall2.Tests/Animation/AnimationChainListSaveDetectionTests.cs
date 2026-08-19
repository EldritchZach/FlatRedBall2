using System.IO;
using System.Linq;
using System.Text;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// FromFile/FromStream/FromString are meant to be single smart entry points that dispatch
// between the .achx (XML) and .achj (JSON) dialects internally (#887), instead of leaving the
// XML-vs-JSON branch to every caller. Path-based dispatch (FromFile) branches on extension;
// text-only dispatch (FromStream/FromString, e.g. clipboard payloads with no path) content-sniffs
// the first non-whitespace character ('{' vs '<'), matching AnimationChain.MonoGame's existing
// FromDetectedStream/LooksLikeJson trick.
public class AnimationChainListSaveDetectionTests
{
    private const string MinimalJson =
        "{\"animationChains\":[{\"name\":\"Walk\",\"frames\":[" +
        "{\"textureName\":\"walk.png\",\"frameLength\":0.1}]}]}";

    private const string MinimalXml =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<AnimationChainArraySave>" +
        "  <AnimationChain><Name>Walk</Name>" +
        "    <Frame><TextureName>walk.png</TextureName><FrameLength>0.1</FrameLength></Frame>" +
        "  </AnimationChain>" +
        "</AnimationChainArraySave>";

    // ─── FromFile: extension-based dispatch ─────────────────────────────────────

    [Fact]
    public void FromFile_AchjExtension_ParsesJsonWithoutCallerBranching()
    {
        var save = AnimationChainListSave.FromFile("Content/Animations/ShmupSpace.achj",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(MinimalJson)));

        save.AnimationChains.Count.ShouldBe(1);
        save.AnimationChains[0].Name.ShouldBe("Walk");
    }

    [Fact]
    public void FromFile_AchxExtension_ParsesXml()
    {
        var save = AnimationChainListSave.FromFile("Content/Animations/ShmupSpace.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(MinimalXml)));

        save.AnimationChains.Count.ShouldBe(1);
        save.AnimationChains[0].Name.ShouldBe("Walk");
    }

    // ─── FromString/FromStream: content-sniffing dispatch ───────────────────────

    [Fact]
    public void FromString_JsonContent_DetectsJsonDialect()
    {
        var save = AnimationChainListSave.FromString(MinimalJson);

        save.AnimationChains.Count.ShouldBe(1);
        save.AnimationChains[0].Name.ShouldBe("Walk");
    }

    [Fact]
    public void FromString_XmlContent_DetectsXmlDialect()
    {
        var save = AnimationChainListSave.FromString(MinimalXml);

        save.AnimationChains.Count.ShouldBe(1);
        save.AnimationChains[0].Name.ShouldBe("Walk");
    }

    [Fact]
    public void FromStream_JsonContent_DetectsJsonDialect()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(MinimalJson));
        var save = AnimationChainListSave.FromStream(stream);

        save.AnimationChains.Count.ShouldBe(1);
        save.AnimationChains[0].Name.ShouldBe("Walk");
    }

    // ─── BOM handling ────────────────────────────────────────────────────────────
    // A leading U+FEFF byte-order-mark character survives into a string when content was
    // decoded via Encoding.UTF8.GetString on raw bytes (rather than StreamReader/File.ReadAllText,
    // which strip it automatically). char.IsWhiteSpace matches U+FEFF as false, so a naive
    // TrimStart()-then-check-first-char sniff misses '{' entirely and misroutes JSON to the XML
    // parser. The sniff must strip a leading BOM char explicitly before inspecting content.

    [Fact]
    public void FromString_JsonWithLeadingBomChar_DetectsJsonDialect()
    {
        var save = AnimationChainListSave.FromString("\uFEFF" + MinimalJson);

        save.AnimationChains.Count.ShouldBe(1);
        save.AnimationChains[0].Name.ShouldBe("Walk");
    }

    [Fact]
    public void FromString_XmlWithLeadingBomChar_DetectsXmlDialect()
    {
        var save = AnimationChainListSave.FromString("\uFEFF" + MinimalXml);

        save.AnimationChains.Count.ShouldBe(1);
        save.AnimationChains[0].Name.ShouldBe("Walk");
    }

    [Fact]
    public void FromStream_JsonBytesWithUtf8Bom_DetectsJsonDialect()
    {
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(MinimalJson))
            .ToArray();
        using var stream = new MemoryStream(bytes);

        var save = AnimationChainListSave.FromStream(stream);

        save.AnimationChains.Count.ShouldBe(1);
        save.AnimationChains[0].Name.ShouldBe("Walk");
    }

    [Fact]
    public void FromStream_XmlBytesWithUtf8Bom_DetectsXmlDialect()
    {
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(MinimalXml))
            .ToArray();
        using var stream = new MemoryStream(bytes);

        var save = AnimationChainListSave.FromStream(stream);

        save.AnimationChains.Count.ShouldBe(1);
        save.AnimationChains[0].Name.ShouldBe("Walk");
    }
}
