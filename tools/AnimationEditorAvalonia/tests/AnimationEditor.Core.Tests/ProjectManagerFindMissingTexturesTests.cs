using AnimationEditor.Core;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ProjectManagerFindMissingTexturesTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // Minimal 24-byte fake PNG (valid signature + IHDR width/height fields).
    private static byte[] MakeFakePng(int width = 64, int height = 64)
    {
        var b = new byte[24];
        b[0] = 0x89; b[1] = 0x50; b[2] = 0x4E; b[3] = 0x47;
        b[4] = 0x0D; b[5] = 0x0A; b[6] = 0x1A; b[7] = 0x0A;
        b[8] = 0; b[9] = 0; b[10] = 0; b[11] = 13;
        b[12] = 0x49; b[13] = 0x48; b[14] = 0x44; b[15] = 0x52;
        b[16] = (byte)(width >> 24); b[17] = (byte)(width >> 16);
        b[18] = (byte)(width >> 8);  b[19] = (byte)width;
        b[20] = (byte)(height >> 24); b[21] = (byte)(height >> 16);
        b[22] = (byte)(height >> 8);  b[23] = (byte)height;
        return b;
    }

    private AnimationChainListSave MakeAcls(params string[] textureNames)
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Idle" };
        foreach (var name in textureNames)
            chain.Frames.Add(new AnimationFrameSave { TextureName = name, FrameLength = 0.1f });
        acls.AnimationChains.Add(chain);
        return acls;
    }

    [Fact]
    public void FindMissingTextures_NoFrames_ReturnsEmpty()
    {
        var pm = new ProjectManager();
        var acls = new AnimationChainListSave();

        var missing = pm.FindMissingTextures(acls, _dir.Path);

        Assert.Empty(missing);
    }

    [Fact]
    public void FindMissingTextures_AllPresent_ReturnsEmpty()
    {
        File.WriteAllBytes(Path.Combine(_dir.Path, "hero.png"), MakeFakePng());
        var pm = new ProjectManager();
        var acls = MakeAcls("hero.png");

        var missing = pm.FindMissingTextures(acls, _dir.Path);

        Assert.Empty(missing);
    }

    [Fact]
    public void FindMissingTextures_OneMissing_ReturnsItsName()
    {
        var pm = new ProjectManager();
        var acls = MakeAcls("ghost.png");

        var missing = pm.FindMissingTextures(acls, _dir.Path);

        Assert.Single(missing);
        Assert.Equal("ghost.png", missing[0]);
    }

    [Fact]
    public void FindMissingTextures_CorruptFile_ReturnsItsName()
    {
        File.WriteAllText(Path.Combine(_dir.Path, "corrupt.png"), "not a png");
        var pm = new ProjectManager();
        var acls = MakeAcls("corrupt.png");

        var missing = pm.FindMissingTextures(acls, _dir.Path);

        Assert.Single(missing);
        Assert.Equal("corrupt.png", missing[0]);
    }

    [Fact]
    public void FindMissingTextures_EmptyTextureName_Skipped()
    {
        var pm = new ProjectManager();
        var acls = MakeAcls(string.Empty);

        var missing = pm.FindMissingTextures(acls, _dir.Path);

        Assert.Empty(missing);
    }

    [Fact]
    public void FindMissingTextures_DuplicateTextureName_CheckedOnce()
    {
        // Both frames reference the same missing texture — it should appear once in results.
        var pm = new ProjectManager();
        var acls = MakeAcls("sheet.png", "sheet.png");

        var missing = pm.FindMissingTextures(acls, _dir.Path);

        Assert.Single(missing);
    }
}
