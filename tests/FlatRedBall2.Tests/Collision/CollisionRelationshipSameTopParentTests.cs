using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// Pins the port of FRB1's #2122 fix: physics (Move/Bounce) must not reposition two entities that
// share a top parent (e.g. one Add()'d as a child of the other). Applying physics between them
// would reposition the shared ancestor, which does not change their relative offset, so they'd
// still overlap next frame — an endless repositioning loop. Events must still fire.
public class CollisionRelationshipSameTopParentTests
{
    static Entity CreateEntityWithCircle(float circleX)
    {
        var entity = new Entity();
        entity.Add(new Circle { Radius = 10f, X = circleX });
        return entity;
    }

    [Fact]
    public void RunCollisions_BounceOnCollision_ObjectsShareTopParent_SkipsPhysicsButRaisesEvent()
    {
        var player = CreateEntityWithCircle(circleX: 0f);
        var grabbed = CreateEntityWithCircle(circleX: 5f); // overlaps player's circle (10+10 > 5)
        player.Add(grabbed);

        var rel = new CollisionRelationship<Entity, Entity>(new[] { player }, new[] { grabbed });
        rel.BounceBothOnCollision(1f, 1f, 1f);
        int occurredCount = 0;
        rel.CollisionOccurred += (_, _) => occurredCount++;

        rel.RunCollisions();

        occurredCount.ShouldBe(1);
        player.Position.ShouldBe(System.Numerics.Vector2.Zero);
        grabbed.Position.ShouldBe(System.Numerics.Vector2.Zero);
    }

    [Fact]
    public void RunCollisions_MoveBothOnCollision_ObjectsDoNotShareTopParent_AppliesPhysics()
    {
        var first = CreateEntityWithCircle(circleX: 0f);
        var second = CreateEntityWithCircle(circleX: 5f); // overlaps first's circle (10+10 > 5)

        var rel = new CollisionRelationship<Entity, Entity>(new[] { first }, new[] { second });
        rel.MoveBothOnCollision(1f, 1f);

        rel.RunCollisions();

        first.Position.ShouldNotBe(System.Numerics.Vector2.Zero);
    }

    [Fact]
    public void RunCollisions_MoveBothOnCollision_ObjectsShareTopParent_SkipsPhysicsButRaisesEvent()
    {
        var player = CreateEntityWithCircle(circleX: 0f);
        var grabbed = CreateEntityWithCircle(circleX: 5f); // overlaps player's circle (10+10 > 5)
        player.Add(grabbed);

        var rel = new CollisionRelationship<Entity, Entity>(new[] { player }, new[] { grabbed });
        rel.MoveBothOnCollision(1f, 1f);
        int occurredCount = 0;
        rel.CollisionOccurred += (_, _) => occurredCount++;

        rel.RunCollisions();

        occurredCount.ShouldBe(1);
        player.Position.ShouldBe(System.Numerics.Vector2.Zero);
        grabbed.Position.ShouldBe(System.Numerics.Vector2.Zero);
    }
}
