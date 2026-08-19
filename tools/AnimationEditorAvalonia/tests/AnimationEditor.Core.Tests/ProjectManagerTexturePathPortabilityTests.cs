using AnimationEditor.Core;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

// #936: a texture set before the project's first save has no achx folder to relativize
// against, so ComputeStorePath (correctly) leaves it absolute -- but nothing ever re-resolved
// that absolute path once a folder became known, so it stayed absolute (and OS-native-slashed)
// forever, mixed in with properly-relative forward-slashed entries from other frames. Saving
// must self-heal: every frame's TextureName is re-normalized against the save target's folder
// each time SaveAnimationChainList(string) runs.
public class ProjectManagerTexturePathPortabilityTests
{
    [Fact]
    public void SaveAnimationChainList_AbsoluteTextureNameInSaveFolder_RewrittenToSimpleRelative()
    {
        var pm = new ProjectManager();
        using var dir = new TestHelpers.TempDir();
        var achxPath = dir.Path + "/Props.achx";

        var frame = new AnimationFrameSave { TextureName = dir.Path.Replace('/', '\\') + @"\items.png" };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(chain);

        pm.SaveAnimationChainList(achxPath);

        var reloaded = AnimationChainListSave.FromFile(achxPath);
        Assert.Equal("items.png", reloaded.AnimationChains.Single().Frames.Single().TextureName);
    }

    [Fact]
    public void SaveAnimationChainList_AbsoluteTextureNameInSubfolder_RewrittenToForwardSlashRelative()
    {
        var pm = new ProjectManager();
        using var dir = new TestHelpers.TempDir();
        var achxPath = dir.Path + "/Props.achx";
        var absoluteTexture = dir.Path.Replace('/', '\\') + @"\Sprites\items.png";

        var frame = new AnimationFrameSave { TextureName = absoluteTexture };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(chain);

        pm.SaveAnimationChainList(achxPath);

        var reloaded = AnimationChainListSave.FromFile(achxPath);
        Assert.Equal("Sprites/items.png", reloaded.AnimationChains.Single().Frames.Single().TextureName);
    }

    [Fact]
    public void SaveAnimationChainList_MixedAbsoluteAndRelativeTextureNames_AllForwardSlashAfterSave()
    {
        var pm = new ProjectManager();
        using var dir = new TestHelpers.TempDir();
        var achxPath = dir.Path + "/Props.achx";

        var frameAbsolute = new AnimationFrameSave { TextureName = dir.Path.Replace('/', '\\') + @"\items.png" };
        var frameRelative = new AnimationFrameSave { TextureName = "items.png" };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frameAbsolute);
        chain.Frames.Add(frameRelative);
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(chain);

        pm.SaveAnimationChainList(achxPath);

        var reloadedFrames = AnimationChainListSave.FromFile(achxPath).AnimationChains.Single().Frames;
        Assert.All(reloadedFrames, f => Assert.Equal("items.png", f.TextureName));
    }

    [Fact]
    public void SaveAnimationChainList_RewritesInMemoryTextureName_NotJustSavedFile()
    {
        var pm = new ProjectManager();
        using var dir = new TestHelpers.TempDir();
        var achxPath = dir.Path + "/Props.achx";

        var frame = new AnimationFrameSave { TextureName = dir.Path.Replace('/', '\\') + @"\items.png" };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(chain);

        pm.SaveAnimationChainList(achxPath);

        // The next save (or any in-memory consumer, e.g. the property panel) must see the
        // healed relative path too, not just the bytes written to disk.
        Assert.Equal("items.png", frame.TextureName);
    }

    [Fact]
    public void SaveAnimationChainList_TextureNameCasePreserved_WhenRewrittenToRelative()
    {
        var pm = new ProjectManager();
        using var dir = new TestHelpers.TempDir();
        var achxPath = dir.Path + "/Props.achx";

        var frame = new AnimationFrameSave { TextureName = dir.Path.Replace('/', '\\') + @"\MyStuff\Hero.PNG" };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(chain);

        pm.SaveAnimationChainList(achxPath);

        var reloaded = AnimationChainListSave.FromFile(achxPath);
        Assert.Equal("MyStuff/Hero.PNG", reloaded.AnimationChains.Single().Frames.Single().TextureName);
    }

    [Fact]
    public void SaveAnimationChainList_AlreadyRelativeBackslashTextureName_NormalizedToForwardSlash()
    {
        var pm = new ProjectManager();
        using var dir = new TestHelpers.TempDir();
        var achxPath = dir.Path + "/Props.achx";

        var frame = new AnimationFrameSave { TextureName = @"Sprites\items.png" };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(chain);

        pm.SaveAnimationChainList(achxPath);

        var reloaded = AnimationChainListSave.FromFile(achxPath);
        Assert.Equal("Sprites/items.png", reloaded.AnimationChains.Single().Frames.Single().TextureName);
    }
}
