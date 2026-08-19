using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Verifies the UV-coordinate gate in OpenAchxWorkflowAsync:
/// UV files with all textures present prompt for conversion; UV files
/// with missing textures are refused; pixel files load normally.
/// </summary>
[Collection("SequentialSingletons")]
public class OpenAchxWorkflowUvGateTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir;
    private readonly TestServices _ctx;

    public OpenAchxWorkflowUvGateTests()
    {
        _dir = new TestHelpers.TempDir();
        _ctx = TestHelpers.SetupFreshAcls();
    }

    public void Dispose() => _dir.Dispose();

    // Minimal 24-byte fake PNG (valid signature + IHDR width/height).
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

    private string WriteUvAchx(string chainName, string textureName)
    {
        var path = Path.Combine(_dir.Path, "test.achx");
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var chain = new AnimationChainSave { Name = chainName };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName      = textureName,
            LeftCoordinate   = 0.25f,
            RightCoordinate  = 0.50f,
            TopCoordinate    = 0.0f,
            BottomCoordinate = 0.25f,
            FrameLength      = 0.1f,
        });
        acls.AnimationChains.Add(chain);
        acls.Save(path);
        return path;
    }

    private string WritePixelAchx(string textureName)
    {
        var path = Path.Combine(_dir.Path, "pixel.achx");
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName      = textureName,
            LeftCoordinate   = 16f,
            RightCoordinate  = 32f,
            TopCoordinate    = 0f,
            BottomCoordinate = 16f,
            FrameLength      = 0.1f,
        });
        acls.AnimationChains.Add(chain);
        acls.Save(path);
        return path;
    }

    // ── UV file, all textures present, user confirms ──────────────────────────

    [Fact]
    public async Task OpenAchxWorkflowAsync_UvFile_AllPresent_UserConfirms_LoadsFile()
    {
        File.WriteAllBytes(Path.Combine(_dir.Path, "sheet.png"), MakeFakePng());
        var achxPath = WriteUvAchx("Walk", "sheet.png");
        _ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);

        await _ctx.AppCommands.OpenAchxWorkflowAsync(achxPath);

        Assert.NotNull(_ctx.ProjectManager.AnimationChainListSave);
        Assert.Equal("Walk", _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name);
    }

    [Fact]
    public async Task OpenAchxWorkflowAsync_UvFile_AllPresent_UserConfirms_SetsOnDiskCoordinateTypeToPixel()
    {
        File.WriteAllBytes(Path.Combine(_dir.Path, "sheet.png"), MakeFakePng());
        var achxPath = WriteUvAchx("Walk", "sheet.png");
        _ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);

        await _ctx.AppCommands.OpenAchxWorkflowAsync(achxPath);

        Assert.Equal(TextureCoordinateType.Pixel, _ctx.ProjectManager.OnDiskCoordinateType);
    }

    [Fact]
    public async Task OpenAchxWorkflowAsync_UvFile_AllPresent_UserConfirms_FiresSuccessEvents()
    {
        File.WriteAllBytes(Path.Combine(_dir.Path, "sheet.png"), MakeFakePng());
        var achxPath = WriteUvAchx("Walk", "sheet.png");
        _ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        bool achxLoaded = false;
        bool currentFileChanged = false;
        _ctx.ApplicationEvents.AchxLoaded += _ => achxLoaded = true;
        _ctx.ApplicationEvents.CurrentFileChanged += _ => currentFileChanged = true;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(achxPath);

        Assert.True(achxLoaded);
        Assert.True(currentFileChanged);
    }

    // ── UV file, all textures present, user declines ──────────────────────────

    [Fact]
    public async Task OpenAchxWorkflowAsync_UvFile_AllPresent_UserDeclines_DoesNotLoadFile()
    {
        File.WriteAllBytes(Path.Combine(_dir.Path, "sheet.png"), MakeFakePng());
        var achxPath = WriteUvAchx("Walk", "sheet.png");
        _ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(false);
        var sentinel = new AnimationChainListSave();
        _ctx.ProjectManager.AnimationChainListSave = sentinel;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(achxPath);

        Assert.Same(sentinel, _ctx.ProjectManager.AnimationChainListSave);
    }

    [Fact]
    public async Task OpenAchxWorkflowAsync_UvFile_AllPresent_UserDeclines_DoesNotFireSuccessEvents()
    {
        File.WriteAllBytes(Path.Combine(_dir.Path, "sheet.png"), MakeFakePng());
        var achxPath = WriteUvAchx("Walk", "sheet.png");
        _ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(false);
        bool anyEventFired = false;
        _ctx.ApplicationEvents.AchxLoaded += _ => anyEventFired = true;
        _ctx.ApplicationEvents.CurrentFileChanged += _ => anyEventFired = true;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(achxPath);

        Assert.False(anyEventFired);
    }

    // ── UV file, missing texture ──────────────────────────────────────────────

    [Fact]
    public async Task OpenAchxWorkflowAsync_UvFile_MissingTexture_FiresLoadFailed()
    {
        var achxPath = WriteUvAchx("Walk", "ghost.png"); // ghost.png not written to disk
        bool loadFailedFired = false;
        _ctx.AppCommands.LoadFailed += (_, _) => loadFailedFired = true;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(achxPath);

        Assert.True(loadFailedFired);
    }

    [Fact]
    public async Task OpenAchxWorkflowAsync_UvFile_MissingTexture_ErrorMessageContainsMissingFileName()
    {
        var achxPath = WriteUvAchx("Walk", "ghost.png");
        string? errorMessage = null;
        _ctx.AppCommands.LoadFailed += (_, ex) => errorMessage = ex.Message;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(achxPath);

        Assert.NotNull(errorMessage);
        Assert.Contains("ghost.png", errorMessage);
    }

    [Fact]
    public async Task OpenAchxWorkflowAsync_UvFile_MissingTexture_DoesNotLoadFile()
    {
        var achxPath = WriteUvAchx("Walk", "ghost.png");
        var sentinel = new AnimationChainListSave();
        _ctx.ProjectManager.AnimationChainListSave = sentinel;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(achxPath);

        Assert.Same(sentinel, _ctx.ProjectManager.AnimationChainListSave);
    }

    [Fact]
    public async Task OpenAchxWorkflowAsync_UvFile_MissingTexture_DoesNotShowConfirmPrompt()
    {
        var achxPath = WriteUvAchx("Walk", "ghost.png");
        bool promptShown = false;
        _ctx.AppCommands.ConfirmAsync = (_, _) => { promptShown = true; return Task.FromResult(true); };

        await _ctx.AppCommands.OpenAchxWorkflowAsync(achxPath);

        Assert.False(promptShown);
    }

    // ── Pixel file, missing texture ───────────────────────────────────────────

    [Fact]
    public async Task OpenAchxWorkflowAsync_PixelFile_MissingTexture_LoadsNormally()
    {
        // ghost.png intentionally not written to disk
        var achxPath = WritePixelAchx("ghost.png");

        await _ctx.AppCommands.OpenAchxWorkflowAsync(achxPath);

        Assert.NotNull(_ctx.ProjectManager.AnimationChainListSave);
        Assert.Equal("Run", _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name);
    }

    [Fact]
    public async Task OpenAchxWorkflowAsync_PixelFile_MissingTexture_DoesNotShowConfirmPrompt()
    {
        var achxPath = WritePixelAchx("ghost.png");
        bool promptShown = false;
        _ctx.AppCommands.ConfirmAsync = (_, _) => { promptShown = true; return Task.FromResult(true); };

        await _ctx.AppCommands.OpenAchxWorkflowAsync(achxPath);

        Assert.False(promptShown);
    }
}
