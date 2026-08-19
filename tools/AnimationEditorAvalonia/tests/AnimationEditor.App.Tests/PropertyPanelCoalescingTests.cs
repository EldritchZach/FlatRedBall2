using AnimationEditor.Core;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression coverage for #897's mouse-wheel repro: scrolling a Prop* NumericUpDown fires one
/// <c>ValueChanged</c> per notch with no <c>LostFocus</c> in between, but every commit also raises
/// <c>AnimationChainsChanged</c>, which <c>HandleAnimationChainsChanged</c> answers with
/// <c>Dispatcher.UIThread.InvokeAsync(RefreshPropertyPanel)</c> (see
/// <see cref="UndoBypassRegressionTests.Undo_AfterFlip_ResyncsFlipToggleButton"/> for that same
/// wiring). In real use that queued job runs on the UI thread well before the next wheel notch
/// arrives, so a naive "seal on every RefreshPropertyPanel call" breaks coalescing on *every*
/// tick — each edit seals the one right after it. These tests pump the dispatcher queue
/// (<see cref="Dispatcher.UIThread.RunJobs"/>) between edits specifically to surface that.
/// </summary>
public class PropertyPanelCoalescingTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.SelectedState.SelectedChain = null;

        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    private static AnimationChainSave MakeChain(TestServices ctx, string name, int frameCount)
    {
        var chain = new AnimationChainSave { Name = name };
        for (int i = 0; i < frameCount; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = $"f{i}.png", ShapesSave = new ShapesSave() });
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        return chain;
    }

    [AvaloniaFact]
    public void PropRelX_ScrollWheelSimulatedAsValueChangedTicksWithDispatcherPumpedBetween_CoalescesIntoSingleUndoEntry()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = MakeChain(ctx, "Walk", 1);
            var frame = chain.Frames[0];
            frame.RelativeX = 0f;
            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();

            var propRelX = window.FindControl<NumericUpDown>("PropRelX")!;

            // Each tick fires ValueChanged -> ApplyFrameRelative -> AppCommands.SetFrameRelative,
            // which raises AnimationChainsChanged -> queues RefreshPropertyPanel. Pumping the
            // dispatcher after each tick lets that queued refresh actually run before the next
            // tick, exactly as it would in a live wheel-scroll session.
            for (decimal v = 1m; v <= 5m; v++)
            {
                propRelX.Value = v;
                Dispatcher.UIThread.RunJobs();
            }

            Assert.Equal(5f, frame.RelativeX);
            Assert.Single(ctx.UndoManager.UndoHistory);

            ctx.UndoManager.Undo();
            Assert.Equal(0f, frame.RelativeX);
        }
        finally { window.Close(); }
    }
}
