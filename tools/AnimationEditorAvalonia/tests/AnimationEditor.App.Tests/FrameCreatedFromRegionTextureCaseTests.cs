using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System;
using System.IO;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression tests for issue #941: a frame created via Ctrl+click / magic-wand
/// (<c>MainWindow.OnFrameCreatedFromRegion</c>) must store the texture's exact on-disk
/// case, not the lowercased cache-key form that <see cref="WireframeControl.LoadedTexturePath"/>
/// exposes for comparison purposes.
/// </summary>
public class FrameCreatedFromRegionTextureCaseTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new System.Collections.Generic.List<object>();
        ctx.AppCommands.ConfirmAsync              = (_, _) => System.Threading.Tasks.Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    private static WireframeControl GetWireframe(MainWindow w)
        => w.FindControl<WireframeControl>("WireframeCtrl")
           ?? throw new InvalidOperationException("WireframeCtrl not found");

    private static string WriteSolidPng(string dir, string name, SKColor color, int size = 64)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    [AvaloniaFact]
    public void CtrlClick_MixedCaseOnDiskTexture_NewFrameKeepsExactOnDiskCase()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            // On-disk filename is mixed-case; the bug lowercased it via LoadedTexturePath.
            var png  = WriteSolidPng(dir, "Items.png", SKColors.Red, size: 64);
            var achx = Path.Combine(dir, "test.achx");
            ctx.ProjectManager.FileName = achx;

            var chain = new AnimationChainSave { Name = "Idle" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;

            var wireframe = GetWireframe(window);
            wireframe.LoadTexture(png);
            wireframe.SetCamera(0f, 0f, 1f);

            wireframe.SimulatePlainCtrlClick(32, 32);

            Assert.Single(chain.Frames);
            Assert.EndsWith("Items.png", chain.Frames[0].TextureName);
        }
        finally
        {
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedChain = null;
            ctx.ProjectManager.FileName     = string.Empty;
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void CtrlClick_SecondFrameOnSameChain_AlsoKeepsExactOnDiskCase()
    {
        // Issue #941's actual repro: the FIRST frame (via drag-drop) gets the right case;
        // a SECOND frame added via Ctrl+click on the same already-loaded texture regressed.
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var png  = WriteSolidPng(dir, "Items.png", SKColors.Red, size: 64);
            var achx = Path.Combine(dir, "test.achx");
            ctx.ProjectManager.FileName = achx;

            var chain = new AnimationChainSave { Name = "Idle" };
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName = "Items.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
            });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;

            var wireframe = GetWireframe(window);
            wireframe.LoadTexture(png);
            wireframe.SetCamera(0f, 0f, 1f);

            wireframe.SimulatePlainCtrlClick(32, 32);

            Assert.Equal(2, chain.Frames.Count);
            Assert.EndsWith("Items.png", chain.Frames[1].TextureName);
        }
        finally
        {
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedChain = null;
            ctx.ProjectManager.FileName     = string.Empty;
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
