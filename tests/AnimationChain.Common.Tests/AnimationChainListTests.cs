using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

public class AnimationChainListTests
{
    private class TestFrame : AnimationFrameBase { }

    private static TestFrame BuildFrame(AnimationFrameSave save) => new()
    {
        TextureName = save.TextureName,
        FrameLength = TimeSpan.FromSeconds(save.FrameLength),
    };

    // In-memory file system so TryReloadFrom's stream-provider seam never touches disk.
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    private void WriteXml(string path, params (string ChainName, int FrameCount)[] chains)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?><AnimationChainArraySave>");
        foreach (var (chainName, frameCount) in chains)
        {
            sb.Append($"<AnimationChain><Name>{chainName}</Name>");
            for (int i = 0; i < frameCount; i++)
                sb.Append($"<Frame><TextureName>{chainName}.png</TextureName><FrameLength>0.1</FrameLength></Frame>");
            sb.Append("</AnimationChain>");
        }
        sb.Append("</AnimationChainArraySave>");
        _files[path] = Encoding.UTF8.GetBytes(sb.ToString());
    }

    private Stream OpenFile(string path) => new MemoryStream(_files[path]);

    [Fact]
    public void GetOwnedShapeNames_ReturnsNamesFromAllFramesAndChains()
    {
        var frameWithShapes = new TestFrame();
        frameWithShapes.Shapes.Add(new AnimationAARectFrame { Name = "Hitbox" });
        var otherFrame = new TestFrame();
        otherFrame.Shapes.Add(new AnimationCircleFrame { Name = "Hurtbox" });

        var list = new AnimationChainList<TestFrame>
        {
            new AnimationChain<TestFrame> { frameWithShapes },
            new AnimationChain<TestFrame> { otherFrame },
        };

        list.GetOwnedShapeNames().ShouldBe(new[] { "Hitbox", "Hurtbox" }, ignoreOrder: true);
    }

    [Fact]
    public void Indexer_UnknownName_ReturnsNull()
    {
        var list = new AnimationChainList<TestFrame>
        {
            new AnimationChain<TestFrame> { Name = "Walk" },
        };

        list["Run"].ShouldBeNull();
    }

    [Fact]
    public void Indexer_KnownName_ReturnsMatchingChain()
    {
        var walk = new AnimationChain<TestFrame> { Name = "Walk" };
        var list = new AnimationChainList<TestFrame> { walk };

        list["Walk"].ShouldBeSameAs(walk);
    }

    [Fact]
    public void TryReloadFrom_InvalidXml_ReturnsFalse()
    {
        var list = new AnimationChainList<TestFrame>();
        _files["bad.achx"] = Encoding.UTF8.GetBytes("not valid xml");

        list.TryReloadFrom("bad.achx", OpenFile, BuildFrame).ShouldBeFalse();
    }

    [Fact]
    public void TryReloadFrom_SameChainName_PreservesInstanceIdentityAndReplacesFrames()
    {
        WriteXml("anim.achx", ("Walk", 2));
        var list = new AnimationChainList<TestFrame>();
        list.TryReloadFrom("anim.achx", OpenFile, BuildFrame);
        var walkBefore = list["Walk"];

        WriteXml("anim.achx", ("Walk", 5));
        list.TryReloadFrom("anim.achx", OpenFile, BuildFrame).ShouldBeTrue();

        list["Walk"].ShouldBeSameAs(walkBefore);
        list["Walk"]!.Count.ShouldBe(5);
    }

    [Fact]
    public void TryReloadFrom_NewChainInSource_AppendedToList()
    {
        WriteXml("anim.achx", ("Walk", 2));
        var list = new AnimationChainList<TestFrame>();
        list.TryReloadFrom("anim.achx", OpenFile, BuildFrame);

        WriteXml("anim.achx", ("Walk", 2), ("Run", 3));
        list.TryReloadFrom("anim.achx", OpenFile, BuildFrame).ShouldBeTrue();

        list.Count.ShouldBe(2);
        list["Run"]!.Count.ShouldBe(3);
    }
}
