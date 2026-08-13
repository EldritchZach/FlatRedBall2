using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class SetFrameTextureNameUndoTests
{
    // ── SetFrameTextureName + Undo ────────────────────────────────────────────

    [Fact]
    public void SetFrameTextureName_Undo_RestoresOldTextureName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame("old.png");
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameTextureName(frame, "new.png");
        Assert.Equal("new.png", frame.TextureName);

        ctx.UndoManager.Undo();

        Assert.Equal("old.png", frame.TextureName);
    }

    [Fact]
    public void SetFrameTextureName_UndoThenRedo_ReappliesNewName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame("original.png");
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameTextureName(frame, "updated.png");
        ctx.UndoManager.Undo();
        Assert.Equal("original.png", frame.TextureName);

        ctx.UndoManager.Redo();

        Assert.Equal("updated.png", frame.TextureName);
    }

    // ── SetFrameTextureName (bulk) — multi-select ─────────────────────────────

    /// <summary>
    /// Issue #860: unlike every other frame property (FrameLength, RelativeX/Y, RGBA,
    /// ColorOperation, pixel region, Flip — all via #571), TextureName had no bulk overload,
    /// so editing it with multiple frames selected only ever touched the primary frame.
    /// </summary>
    [Fact]
    public void SetFrameTextureName_MultipleFrames_AppliesToAllAsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 2);
        var frames = chain.Frames.ToList();

        ctx.AppCommands.SetFrameTextureName(frames, "shared.png");

        Assert.All(frames, f => Assert.Equal("shared.png", f.TextureName));
        Assert.Single(ctx.UndoManager.UndoHistory);

        ctx.UndoManager.Undo();

        Assert.Equal("frame0.png", frames[0].TextureName);
        Assert.Equal("frame1.png", frames[1].TextureName);
    }
}
