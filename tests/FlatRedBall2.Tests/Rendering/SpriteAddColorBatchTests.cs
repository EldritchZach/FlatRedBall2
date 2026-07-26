using FlatRedBall2.Animation;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

public class SpriteAddColorBatchTests
{
    private static Sprite MakeSpritePlayingAddFrame()
    {
        var chain = new AnimationChain { Name = "Flash" };
        chain.Add(new AnimationFrame { ColorOperation = ColorOperation.Add, Red = 200 });
        var chainList = new AnimationChainList { chain };

        var sprite = new Sprite { AnimationChains = chainList };
        sprite.PlayAnimation("Flash");
        return sprite;
    }

    private class FakeBatch : IRenderBatch
    {
        public bool FlipsY => true;
        public void Begin(SpriteBatch spriteBatch, Camera camera) { }
        public void End(SpriteBatch spriteBatch) { }
    }

    [Fact]
    public void Batch_AddFrameOnDefaultWorldSpaceBatch_ReturnsWorldSpaceAddColorBatch()
    {
        var sprite = MakeSpritePlayingAddFrame();

        sprite.Batch.ShouldBeSameAs(WorldSpaceAddColorBatch.Instance);
    }

    [Fact]
    public void Batch_AddFrameOnScreenSpaceBatch_ReturnsScreenSpaceAddColorBatch()
    {
        var sprite = MakeSpritePlayingAddFrame();
        sprite.Batch = ScreenSpaceBatch.Instance;

        sprite.Batch.ShouldBeSameAs(ScreenSpaceAddColorBatch.Instance);
    }

    [Fact]
    public void Batch_AddFrameOnCustomBatch_ReturnsCustomBatchUnchanged()
    {
        var sprite = MakeSpritePlayingAddFrame();
        var custom = new FakeBatch();
        sprite.Batch = custom;

        sprite.Batch.ShouldBeSameAs(custom);
    }

    [Fact]
    public void Batch_NoAnimation_ReturnsAssignedBatch()
    {
        var sprite = new Sprite();

        sprite.Batch.ShouldBeSameAs(WorldSpaceBatch.Instance);
    }
}
