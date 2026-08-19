using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="AppCommands.ResolveRegionFrameTextureName"/> -- the pure logic behind
/// the browser build's create-frame-from-region handler (<c>AnimationEditor.Browser/App.axaml.cs</c>),
/// extracted so it's testable without a WASM host (issue #941).
/// </summary>
public class AppCommandsResolveRegionFrameTextureNameTests
{
    [Fact]
    public void SelectedFrameHasTexture_ReturnsSelectedFrameTextureExactCase()
    {
        var chain = new AnimationChainSave();
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Chain.PNG" });
        var selected = new AnimationFrameSave { TextureName = "Selected.PNG" };

        var result = AppCommands.ResolveRegionFrameTextureName(selected, chain);

        Assert.Equal("Selected.PNG", result);
    }

    [Fact]
    public void NoSelectedFrame_ReturnsFirstChainFrameTextureExactCase()
    {
        var chain = new AnimationChainSave();
        chain.Frames.Add(new AnimationFrameSave { TextureName = "First.PNG" });

        var result = AppCommands.ResolveRegionFrameTextureName(null, chain);

        Assert.Equal("First.PNG", result);
    }

    [Fact]
    public void NoSelectedFrameAndEmptyChain_ReturnsNull()
    {
        var chain = new AnimationChainSave();

        var result = AppCommands.ResolveRegionFrameTextureName(null, chain);

        Assert.Null(result);
    }
}
