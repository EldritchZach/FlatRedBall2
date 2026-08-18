using AnimationEditor.Core.CommandsAndState.Commands;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Do/Undo/Description for the whole-animation offset drag command (issue #912: drag an entire
/// chain's frames together in the Preview panel, each frame keeping its own starting offset).
/// </summary>
public class MoveFrameOffsetBulkCommandTests
{
    [Fact]
    public void Description_ReturnsMoveAnimation()
    {
        var expected = "Move Animation";
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var snapshots = new List<MoveFrameOffsetBulkCommand.FrameSnapshot>
        {
            new(chain.Frames[0], OldX: 0f, OldY: 0f, NewX: 5f, NewY: 3f),
            new(chain.Frames[1], OldX: 1f, OldY: 1f, NewX: 6f, NewY: 4f),
        };
        var cmd = new MoveFrameOffsetBulkCommand(snapshots, ctx.AppCommands, ctx.ApplicationEvents);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void Do_AppliesNewOffsetToEveryFrame_PreservingEachFramesOwnDelta()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var frameA = chain.Frames[0];
        var frameB = chain.Frames[1];
        frameA.RelativeX = 0f;  frameA.RelativeY = 0f;
        frameB.RelativeX = 10f; frameB.RelativeY = -5f;
        var snapshots = new List<MoveFrameOffsetBulkCommand.FrameSnapshot>
        {
            new(frameA, OldX: 0f,  OldY: 0f,  NewX: 3f,  NewY: 2f),
            new(frameB, OldX: 10f, OldY: -5f, NewX: 13f, NewY: -3f),
        };
        var cmd = new MoveFrameOffsetBulkCommand(snapshots, ctx.AppCommands, ctx.ApplicationEvents);

        cmd.Do();

        Assert.Equal(3f, frameA.RelativeX, precision: 3);
        Assert.Equal(2f, frameA.RelativeY, precision: 3);
        Assert.Equal(13f, frameB.RelativeX, precision: 3);
        Assert.Equal(-3f, frameB.RelativeY, precision: 3);
    }

    [Fact]
    public void Undo_RestoresEachFramesOwnOriginalOffset()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var frameA = chain.Frames[0];
        var frameB = chain.Frames[1];
        frameA.RelativeX = 3f;  frameA.RelativeY = 2f;
        frameB.RelativeX = 13f; frameB.RelativeY = -3f;
        var snapshots = new List<MoveFrameOffsetBulkCommand.FrameSnapshot>
        {
            new(frameA, OldX: 0f,  OldY: 0f,  NewX: 3f,  NewY: 2f),
            new(frameB, OldX: 10f, OldY: -5f, NewX: 13f, NewY: -3f),
        };
        var cmd = new MoveFrameOffsetBulkCommand(snapshots, ctx.AppCommands, ctx.ApplicationEvents);

        cmd.Undo();

        Assert.Equal(0f, frameA.RelativeX, precision: 3);
        Assert.Equal(0f, frameA.RelativeY, precision: 3);
        Assert.Equal(10f, frameB.RelativeX, precision: 3);
        Assert.Equal(-5f, frameB.RelativeY, precision: 3);
    }
}
