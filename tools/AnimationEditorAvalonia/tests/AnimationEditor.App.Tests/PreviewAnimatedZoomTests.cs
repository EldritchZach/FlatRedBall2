using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using Avalonia;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Smooth (animated) wheel zoom for the bottom Preview panel (#451). Mirrors
/// <see cref="WireframeAnimatedZoomTests"/>: the wheel handler retargets a single <c>_zoomTarget</c>
/// and eases toward it via <see cref="ZoomChase"/>, applying each stepped value through the
/// pivot-preserving <c>ZoomToward</c>.
///
/// The pure easing is covered in <c>ZoomChaseTests</c>; these cover the PreviewControl wiring:
/// target computation, retargeting in-flight, exact settle, and that the cursor pivot stays
/// anchored for the whole animation (not just at the end).
/// </summary>
public class PreviewAnimatedZoomTests
{
    private static readonly int[] _presets = { 50, 100, 150, 200 };

    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.AppCommands.DoOnUiThread              = a => a();
        return ctx;
    }

    private static PreviewControl MakeControl(TestServices ctx)
    {
        var ctrl = ctx.CreatePreviewControl();
        ctrl.Measure(new Size(400, 300));
        ctrl.Arrange(new Rect(0, 0, 400, 300));
        ctrl.WheelZoomPresets = _presets;
        return ctrl;
    }

    [AvaloniaFact]
    public void SimulateWheelZoomBegin_RapidNotches_RetargetsToAccumulatedPreset()
    {
        var ctrl = MakeControl(ResetSingletons());
        ctrl.SetZoomPercent(100);

        ctrl.SimulateWheelZoomBegin(200f, 150f, zoomIn: true);   // target 150 %
        ctrl.StepZoomAnimation(0.016f);                          // ease partway toward 150 %
        ctrl.SimulateWheelZoomBegin(200f, 150f, zoomIn: true);   // second notch before arrival

        // The second notch must step the TARGET to the next preset (200 %), not re-step
        // from the mid-animation zoom (which would re-target 150 %).
        Assert.Equal(2.0f, ctrl.TargetZoom, precision: 5);

        ctrl.SettleZoomAnimation();
        Assert.Equal(2.0f, ctrl.Zoom, precision: 5);
        Assert.False(ctrl.IsZoomAnimating);
    }

    [AvaloniaFact]
    public void SimulateWheelZoomBegin_ZoomIn_TargetsNextPresetAndEasesPartway()
    {
        var ctrl = MakeControl(ResetSingletons());
        ctrl.SetZoomPercent(100);

        ctrl.SimulateWheelZoomBegin(200f, 150f, zoomIn: true);

        Assert.True(ctrl.IsZoomAnimating);
        Assert.Equal(1.5f, ctrl.TargetZoom, precision: 5);

        // One 16 ms tick eases toward — but does not reach — the target.
        ctrl.StepZoomAnimation(0.016f);
        float z = ctrl.Zoom;
        Assert.True(z > 1.0f && z < 1.5f, $"expected zoom in (1.0, 1.5) after one tick, got {z}");

        ctrl.SettleZoomAnimation(); // clean up the timer
    }

    [AvaloniaFact]
    public void StepZoomAnimation_PivotStaysAnchoredDuringAnimation()
    {
        var ctrl = MakeControl(ResetSingletons());
        // Known camera: pan (0,0), zoom 1×. The world point under an off-centre pivot must not
        // move while the zoom eases toward its target.
        ctrl.SetZoomPercent(100);
        ctrl.SetPan(0f, 0f);
        const float pivotX = 100f, pivotY = 100f;
        var (worldX, worldY) = ctrl.ScreenToWorldForTest(pivotX, pivotY);

        ctrl.SimulateWheelZoomBegin(pivotX, pivotY, zoomIn: true);
        ctrl.StepZoomAnimation(0.016f); // mid-animation (zoom now between 1.0 and 1.5)

        var (worldAfterX, worldAfterY) = ctrl.ScreenToWorldForTest(pivotX, pivotY);
        Assert.Equal(worldX, worldAfterX, precision: 2);
        Assert.Equal(worldY, worldAfterY, precision: 2);

        ctrl.SettleZoomAnimation();
    }

    [AvaloniaFact]
    public void StepZoomAnimation_RunToCompletion_LandsExactlyOnTargetAndStops()
    {
        var ctrl = MakeControl(ResetSingletons());
        ctrl.SetZoomPercent(100);

        ctrl.SimulateWheelZoomBegin(200f, 150f, zoomIn: true); // target 150 %
        ctrl.SettleZoomAnimation();

        Assert.Equal(1.5f, ctrl.Zoom, precision: 5);
        Assert.False(ctrl.IsZoomAnimating);
    }
}
