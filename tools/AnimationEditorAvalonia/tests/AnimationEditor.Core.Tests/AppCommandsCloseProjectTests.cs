using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Issue #948: File > Close Project must reset the editor to the same blank state as a fresh
// app launch -- ProjectManager, SelectedState, and the undo stack -- so a user can switch
// projects without relaunching, and so #949's before/after memory measurement gets a clean
// baseline between runs.
[Collection("SequentialSingletons")]
public class AppCommandsCloseProjectTests
{
    private readonly TestServices ctx = TestHelpers.SetupFreshAcls();

    [Fact]
    public void CloseProject_CreatesEmptyAcls()
    {
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(
            new AnimationChainSave { Name = "Existing" });

        ctx.AppCommands.CloseProject();

        Assert.NotNull(ctx.ProjectManager.AnimationChainListSave);
        Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
    }

    [Fact]
    public void CloseProject_ClearsFileName()
    {
        ctx.ProjectManager.FileName = TestPaths.Abs("some", "file.achx");

        ctx.AppCommands.CloseProject();

        Assert.Null(ctx.ProjectManager.FileName);
    }

    [Fact]
    public void CloseProject_ClearsProjectFolderPath()
    {
        ctx.ProjectManager.ProjectFolderPath = TestPaths.AbsDir("some", "project");

        ctx.AppCommands.CloseProject();

        Assert.Null(ctx.ProjectManager.ProjectFolderPath);
    }

    [Fact]
    public void CloseProject_ClearsSelectedChain()
    {
        var chain = new AnimationChainSave { Name = "A" };
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedChain = chain;

        ctx.AppCommands.CloseProject();

        Assert.Null(ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public void CloseProject_ClearsUndoStack()
    {
        var chain = new AnimationChainSave { Name = "A" };
        ctx.AppCommands.AddFrame(chain); // pushes an undo entry
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.AppCommands.CloseProject();

        Assert.False(ctx.UndoManager.CanUndo);
        Assert.False(ctx.UndoManager.CanRedo);
    }

    [Fact]
    public void CloseProject_FiresRefreshTreeViewRequested()
    {
        bool fired = false;
        ctx.AppCommands.RefreshTreeViewRequested += () => fired = true;

        ctx.AppCommands.CloseProject();

        Assert.True(fired);
    }

    [Fact]
    public void CloseProject_DeletesRecoveryFile()
    {
        ctx.IoManager.WriteRecoveryFile(ctx.ProjectManager.AnimationChainListSave);
        Assert.True(ctx.IoManager.RecoveryFileExists());

        ctx.AppCommands.CloseProject();

        Assert.False(ctx.IoManager.RecoveryFileExists());
    }
}
