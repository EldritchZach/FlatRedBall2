using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsRecoveryTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir;
    private readonly TestServices ctx;

    public AppCommandsRecoveryTests()
    {
        _dir = new TestHelpers.TempDir();
        ctx = TestHelpers.SetupFreshAcls();
        ctx.IoManager.RecoveryFilePath = _dir.Path + "/recovery.achx";
    }

    public void Dispose() => _dir.Dispose();

    // ── Recovery write via SaveCurrentAnimationChainList ─────────────────────

    [Fact]
    public void SaveCurrentAnimationChainList_WhenFileNameIsNull_WritesRecoveryFile()
    {
        ctx.ProjectManager.FileName = null;
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();

        ctx.AppCommands.SaveCurrentAnimationChainList();

        Assert.True(ctx.IoManager.RecoveryFileExists());
    }

    [Fact]
    public void SaveCurrentAnimationChainList_WhenFileNameIsSet_DoesNotWriteRecoveryFile()
    {
        var target = _dir.Path + "/hero.achx";
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = target;

        ctx.AppCommands.SaveCurrentAnimationChainList();

        Assert.False(ctx.IoManager.RecoveryFileExists());
    }

    [Fact]
    public void SaveCurrentAnimationChainList_WhenFileNameIsNull_RecoveryFileContainsChainData()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Idle" });
        ctx.ProjectManager.AnimationChainListSave = acls;
        ctx.ProjectManager.FileName = null;

        ctx.AppCommands.SaveCurrentAnimationChainList();

        var xml = File.ReadAllText(ctx.IoManager.RecoveryFilePath);
        Assert.Contains("Idle", xml);
    }

    // ── Recovery deletion via SaveCurrentAnimationChainListAsync ─────────────

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenSaved_DeletesRecoveryFile()
    {
        var target = _dir.Path + "/out.achx";
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.AppCommands.FileDialogService = new StubFileDialogService(target);

        // Write a recovery file first
        ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave);
        Assert.True(ctx.IoManager.RecoveryFileExists());

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.False(ctx.IoManager.RecoveryFileExists());
    }

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenDialogCancelled_PreservesRecoveryFile()
    {
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.AppCommands.FileDialogService = new StubFileDialogService(null);

        ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave);
        Assert.True(ctx.IoManager.RecoveryFileExists());

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.True(ctx.IoManager.RecoveryFileExists(), "Recovery should be preserved when user cancels Save As");
    }
}
