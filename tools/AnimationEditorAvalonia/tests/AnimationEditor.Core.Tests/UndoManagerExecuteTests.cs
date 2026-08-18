using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Covers <see cref="UndoManager.Execute"/> — the execute-through-command chokepoint
/// that runs <see cref="IUndoableCommand.Do"/> and records the command in one step.
/// </summary>
public class UndoManagerExecuteTests
{
    private readonly UndoManager _undo = new();

    [Fact]
    public void Execute_RunsDoAndPushesToUndoStack()
    {
        var cmd = new SpyCommand(doResult: true);

        _undo.Execute(cmd);

        Assert.Equal(1, cmd.DoCalls);
        Assert.True(_undo.CanUndo);
    }

    [Fact]
    public void Execute_ClearsRedoStack()
    {
        _undo.Execute(new SpyCommand(doResult: true));
        _undo.Undo();                       // moves the command to the redo stack
        Assert.True(_undo.CanRedo);

        _undo.Execute(new SpyCommand(doResult: true));

        Assert.False(_undo.CanRedo);
    }

    [Fact]
    public void Execute_WhenDoReturnsFalse_DoesNotRecordEntry()
    {
        // Do() returning false means the command was a no-op (e.g. a reorder that
        // produced an identical list) — it must not pollute the undo stack.
        var cmd = new SpyCommand(doResult: false);

        _undo.Execute(cmd);

        Assert.Equal(1, cmd.DoCalls);
        Assert.False(_undo.CanUndo);
    }

    // ── Coalescing (#897 — one undo entry per edit session, not per keystroke) ──

    [Fact]
    public void Execute_CoalesceGroupMatches_MergesIntoSingleEntry()
    {
        _undo.Execute(new SpyCommand(doResult: true, coalesceGroup: "A"));
        _undo.Execute(new SpyCommand(doResult: true, coalesceGroup: "A"));

        Assert.Single(_undo.UndoHistory);
        Assert.Equal(1, ((SpyCommand)_undo.UndoHistory[0]).MergeCount);
    }

    [Fact]
    public void Execute_CoalesceGroupDiffers_DoesNotMerge()
    {
        _undo.Execute(new SpyCommand(doResult: true, coalesceGroup: "A"));
        _undo.Execute(new SpyCommand(doResult: true, coalesceGroup: "B"));

        Assert.Equal(2, _undo.UndoHistory.Count);
    }

    [Fact]
    public void Execute_CoalesceGroupMatchesAfterSealCoalescing_DoesNotMerge()
    {
        _undo.Execute(new SpyCommand(doResult: true, coalesceGroup: "A"));
        _undo.SealCoalescing();
        _undo.Execute(new SpyCommand(doResult: true, coalesceGroup: "A"));

        Assert.Equal(2, _undo.UndoHistory.Count);
    }

    [Fact]
    public void Execute_CoalesceGroupMatchesAfterUndo_DoesNotMerge()
    {
        // Undoing seals the coalescing window -- a fresh edit to the same field afterward must not
        // silently fold into an entry the user just explicitly undid past.
        _undo.Execute(new SpyCommand(doResult: true, coalesceGroup: "A"));
        _undo.Undo();
        _undo.Execute(new SpyCommand(doResult: true, coalesceGroup: "A"));

        Assert.Single(_undo.UndoHistory);
    }

    private sealed class SpyCommand : IUndoableCommand
    {
        private readonly bool _doResult;

        public string Description => "Spy";
        public string? CoalesceGroup { get; }
        public int MergeCount { get; private set; }

        public SpyCommand(bool doResult, string? coalesceGroup = null)
        {
            _doResult = doResult;
            CoalesceGroup = coalesceGroup;
        }

        public int DoCalls { get; private set; }

        public bool Do() { DoCalls++; return _doResult; }
        public void Undo() { }
        public void Redo() { }

        public IUndoableCommand CoalesceWith(IUndoableCommand previous)
        {
            MergeCount = ((SpyCommand)previous).MergeCount + 1;
            return this;
        }
    }
}
