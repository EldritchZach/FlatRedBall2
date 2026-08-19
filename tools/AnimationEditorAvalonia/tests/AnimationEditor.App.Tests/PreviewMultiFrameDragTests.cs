using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for drag-to-offset a multi-selection of individual frames (issue #917): dragging the
/// Preview panel's displayed sprite when 2+ frames are multi-selected (but not a whole chain)
/// shifts only those frames' <see cref="AnimationFrameSave.RelativeX"/>/<see cref="AnimationFrameSave.RelativeY"/>
/// by the same delta, preserving each frame's own starting offset, and leaves other frames (in the
/// same chain or elsewhere) untouched. Mirrors <see cref="PreviewChainDragTests"/>: most cases use
/// <see cref="PreviewControl.SimulateMultiFrameDrag"/>; the routing test drives real pointer input.
/// </summary>
public class PreviewMultiFrameDragTests
{
    private static AnimationFrameSave MakeFrame(float relativeX, float relativeY)
    {
        return new AnimationFrameSave
        {
            FrameLength = 0.1f,
            RelativeX   = relativeX,
            RelativeY   = relativeY,
            ShapesSave  = new ShapesSave()
        };
    }

    // ── Multi-frame drag ────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateMultiFrameDrag_TwoFramesMultiSelected_ShiftsOnlyThoseByDelta_PreservingOwnOffset()
    {
        var ctx     = TestHelpers.BuildServices();
        var frameA  = MakeFrame(relativeX: 0f, relativeY: 0f);
        var frameB  = MakeFrame(relativeX: 10f, relativeY: -5f);
        var frameC  = MakeFrame(relativeX: 20f, relativeY: 7f); // not selected — must stay untouched
        var chain   = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        chain.Frames.Add(frameC);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedNodes = new List<object> { frameA, frameB };

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateMultiFrameDrag(3f, 2f);

        Assert.Equal(3f, frameA.RelativeX, precision: 3);
        Assert.Equal(2f, frameA.RelativeY, precision: 3);
        Assert.Equal(13f, frameB.RelativeX, precision: 3);
        Assert.Equal(-3f, frameB.RelativeY, precision: 3);
        Assert.Equal(20f, frameC.RelativeX, precision: 3);
        Assert.Equal(7f, frameC.RelativeY, precision: 3);
    }

    [AvaloniaFact]
    public void SimulateMultiFrameDrag_AfterRelease_SingleUndoRestoresEverySelectedFramesOwnOffset()
    {
        var ctx    = TestHelpers.BuildServices();
        var frameA = MakeFrame(relativeX: 0f, relativeY: 0f);
        var frameB = MakeFrame(relativeX: 10f, relativeY: -5f);
        var chain  = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedNodes = new List<object> { frameA, frameB };

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateMultiFrameDrag(3f, 2f);

        Assert.True(ctx.UndoManager.CanUndo);
        Assert.Equal("Move Animation", ctx.UndoManager.UndoHistory[^1].Description);

        ctx.UndoManager.Undo();

        Assert.Equal(0f, frameA.RelativeX, precision: 3);
        Assert.Equal(0f, frameA.RelativeY, precision: 3);
        Assert.Equal(10f, frameB.RelativeX, precision: 3);
        Assert.Equal(-5f, frameB.RelativeY, precision: 3);
    }

    [AvaloniaFact]
    public void SimulateMultiFrameDrag_NegligibleDelta_DoesNotRecordUndoStep()
    {
        var ctx    = TestHelpers.BuildServices();
        var frameA = MakeFrame(relativeX: 0f, relativeY: 0f);
        var frameB = MakeFrame(relativeX: 10f, relativeY: -5f);
        var chain  = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedNodes = new List<object> { frameA, frameB };

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateMultiFrameDrag(0f, 0f);

        Assert.False(ctx.UndoManager.CanUndo);
    }

    // ── Gating: a single pinned frame must still use the single-frame path ─────

    [AvaloniaFact]
    public void SimulateMultiFrameDrag_OnlyOneFrameSelected_IsNoOp()
    {
        var ctx    = TestHelpers.BuildServices();
        var frameA = MakeFrame(relativeX: 0f, relativeY: 0f);
        var chain  = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frameA;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateMultiFrameDrag(3f, 2f);

        Assert.Equal(0f, frameA.RelativeX, precision: 3);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    // ── Priority / real pointer routing ─────────────────────────────────────

    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        return ctx;
    }

    private static string WriteSolidPng(string dir, int size = 64, string name = "sprite.png")
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(SKColors.CornflowerBlue);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    [AvaloniaFact]
    public void RealDrag_TwoFramesMultiSelected_ShowsSizeAllCursorAndMovesOnlyThemTogether()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir);
            var frameA = MakeFrame(relativeX: 0f, relativeY: 0f);
            var frameB = MakeFrame(relativeX: 10f, relativeY: -5f);
            frameA.TextureName = texPath;
            frameB.TextureName = texPath;

            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(frameA);
            chain.Frames.Add(frameB);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedNodes = new List<object> { frameA, frameB };
            ctx.SelectedState.SelectedFrame = frameA; // last-clicked frame — pinned, so its sprite renders
            // Pre-warm the bitmap cache the way a real render pass would, so the frame-drag
            // hit-test (which requires a cached bitmap) is actually eligible to fire.
            ctx.ThumbnailService.GetBitmap(texPath);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
            float centerX = (float)((preview.Bounds.Width - 20) / 2 + 20);
            float centerY = (float)((preview.Bounds.Height - 20) / 2 + 20);
            var localPoint  = new Point(centerX, centerY); // frameA sits at world (0,0) == canvas center
            var windowPoint = preview.TranslatePoint(localPoint, window)!.Value;

            Assert.Equal(StandardCursorType.SizeAll, preview.GetHoverCursorTypeForTest(centerX, centerY));

            window.MouseDown(windowPoint, MouseButton.Left);
            var movedPoint = windowPoint + new Point(5, 5);
            window.MouseMove(movedPoint);
            Dispatcher.UIThread.RunJobs();
            window.MouseUp(movedPoint, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.NotEqual(0f, frameA.RelativeX);
            Assert.NotEqual(10f, frameB.RelativeX); // the other multi-selected frame shifted too

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
