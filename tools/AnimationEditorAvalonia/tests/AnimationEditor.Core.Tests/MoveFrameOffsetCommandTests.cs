using AnimationEditor.Core.CommandsAndState.Commands;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Do/Undo/Description for the frame-offset drag command (issue: drag sprite in Preview panel).
/// </summary>
public class MoveFrameOffsetCommandTests
{
    [Fact]
    public void Description_ReturnsMoveFrame()
    {
        var expected = "Move Frame";
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var cmd = new MoveFrameOffsetCommand(
            chain.Frames[0], oldX: 0f, oldY: 0f, newX: 5f, newY: 3f,
            ctx.AppCommands, ctx.ApplicationEvents);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void Do_AppliesNewOffset()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        frame.RelativeX = 1f;
        frame.RelativeY = 2f;
        var cmd = new MoveFrameOffsetCommand(
            frame, oldX: 1f, oldY: 2f, newX: 7f, newY: -4f,
            ctx.AppCommands, ctx.ApplicationEvents);

        cmd.Do();

        Assert.Equal(7f, frame.RelativeX, precision: 3);
        Assert.Equal(-4f, frame.RelativeY, precision: 3);
    }

    [Fact]
    public void Undo_RestoresOldOffset()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        frame.RelativeX = 7f;
        frame.RelativeY = -4f;
        var cmd = new MoveFrameOffsetCommand(
            frame, oldX: 1f, oldY: 2f, newX: 7f, newY: -4f,
            ctx.AppCommands, ctx.ApplicationEvents);

        cmd.Undo();

        Assert.Equal(1f, frame.RelativeX, precision: 3);
        Assert.Equal(2f, frame.RelativeY, precision: 3);
    }

    // ── Undo/Redo must signal a display refresh (#905 follow-up) ──────────────
    // WireframeControl's origin crosshair is driven by RefreshAnimationFrameDisplayRequested
    // (see WireframeControl.InitializeServices), so Ctrl+Z/Ctrl+Y only repaint it if Apply()
    // keeps raising this event -- this guards against a future edit silently dropping the call.

    [Fact]
    public void Do_RaisesRefreshAnimationFrameDisplayRequested()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var cmd = new MoveFrameOffsetCommand(
            chain.Frames[0], oldX: 0f, oldY: 0f, newX: 5f, newY: 3f,
            ctx.AppCommands, ctx.ApplicationEvents);
        bool fired = false;
        ctx.AppCommands.RefreshAnimationFrameDisplayRequested += () => fired = true;

        cmd.Do();

        Assert.True(fired);
    }

    [Fact]
    public void Undo_RaisesRefreshAnimationFrameDisplayRequested()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var cmd = new MoveFrameOffsetCommand(
            chain.Frames[0], oldX: 0f, oldY: 0f, newX: 5f, newY: 3f,
            ctx.AppCommands, ctx.ApplicationEvents);
        bool fired = false;
        ctx.AppCommands.RefreshAnimationFrameDisplayRequested += () => fired = true;

        cmd.Undo();

        Assert.True(fired);
    }
}
