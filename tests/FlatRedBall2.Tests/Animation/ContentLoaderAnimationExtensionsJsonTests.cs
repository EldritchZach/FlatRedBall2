using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FlatRedBall2.Animation.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// ContentLoader.LoadAnimationChainList (the game-code entry point) must dispatch to the JSON
// parser for .achj paths, same as AnimationEditor.Core.ProjectManager already does (#872 follow-up).
public class ContentLoaderAnimationExtensionsJsonTests
{
    [Fact]
    public void LoadAnimationChainList_AchjPath_ParsesJsonIntoRuntimeChainList()
    {
        var svc = new ContentLoader();
        svc.TextureLoader = _ => null!;
        var virtualFiles = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        svc.StreamProvider = path =>
        {
            if (!virtualFiles.TryGetValue(path, out var bytes))
                throw new FileNotFoundException(path);
            return new MemoryStream(bytes);
        };

        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "walk.png", FrameLength = 0.1f });
        save.AnimationChains.Add(chain);
        virtualFiles["anim.achj"] = Encoding.UTF8.GetBytes(save.ToJsonString());

        var list = svc.LoadAnimationChainList("anim.achj");

        list["Walk"].ShouldNotBeNull();
        list["Walk"]!.Count.ShouldBe(1);
    }
}
