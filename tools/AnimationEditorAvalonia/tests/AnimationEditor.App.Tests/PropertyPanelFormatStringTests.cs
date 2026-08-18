using AnimationEditor.Core;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression coverage for #902: whole-pixel Relative X/Y values were displayed with three
/// padded decimal places ("5.000") because of FormatString="0.000" on the Prop* NumericUpDowns.
/// </summary>
public class PropertyPanelFormatStringTests
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

    [AvaloniaFact]
    public void PropRelX_WholeNumberValue_DisplaysWithoutTrailingZeros()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "f0.png", ShapesSave = new ShapesSave(), RelativeX = 5f };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();

            var propRelX = window.FindControl<NumericUpDown>("PropRelX")!;

            Assert.Equal("5", propRelX.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PropRelY_FractionalValue_StillDisplaysDecimals()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "f0.png", ShapesSave = new ShapesSave(), RelativeY = 2.5f };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();

            var propRelY = window.FindControl<NumericUpDown>("PropRelY")!;

            Assert.Equal("2.5", propRelY.Text);
        }
        finally { window.Close(); }
    }
}
