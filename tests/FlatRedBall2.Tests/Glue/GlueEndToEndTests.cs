using System;
using System.IO;
using System.Linq;
using FlatRedBall2.Collision;
using FlatRedBall2.Glue;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// The whole pipeline on real Glue projects: read the files, resolve the graph, build the objects.
// Beefball is the meaningful case — it is shapes-only and tile-free, so every one of its visual
// objects is a type this phase can build.
public class GlueEndToEndTests
{
    private static string Gluj(string project, string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", project, fileName);

    [Fact]
    public void Beefball_GameScreen_BuildsWithoutErrors()
    {
        var result = GlueProjectLoader.Load(Gluj("Beefball", "Beefball.gluj"));
        var gameScreen = result.Project.Screens.Single(s => s.Name == @"Screens\GameScreen");

        var screen = new GlueScreen { Save = gameScreen };
        screen.BuildObjects();

        result.HasErrors.ShouldBeFalse();
        screen.BuildDiagnostics.ShouldNotContain(d => d.Severity == GlueDiagnosticSeverity.Error);
    }

    [Fact]
    public void Beefball_PlayerBall_BuildsBothCirclesAtTheirAuthoredRadius()
    {
        // The payoff case for this phase: a real Glue entity becomes real, sized, attached FRB2
        // objects with no hand-written C# anywhere.
        var result = GlueProjectLoader.Load(Gluj("Beefball", "Beefball.gluj"));
        var playerBall = result.Project.Entities.Single(e => e.Name == @"Entities\PlayerBall");

        var entity = new GlueEntity { X = 40f, Y = 25f, Save = playerBall };
        entity.BuildObjects();

        entity.Objects.Count.ShouldBe(2);

        var circle = (Circle)entity.Objects["CircleInstance"];
        var cooldown = (Circle)entity.Objects["CooldownCircle"];

        circle.Radius.ShouldBe(16f);
        cooldown.Radius.ShouldBe(16f);

        // Both are authored AttachToContainer, so they ride with the entity...
        circle.Parent.ShouldBe(entity);
        cooldown.Parent.ShouldBe(entity);
        circle.AbsoluteX.ShouldBe(40f);
        circle.AbsoluteY.ShouldBe(25f);

        // ...and they are visible, which FRB2 shapes are not by default.
        circle.IsVisible.ShouldBeTrue();
    }

    [Fact]
    public void Beefball_Puck_BuildsItsCircle()
    {
        var result = GlueProjectLoader.Load(Gluj("Beefball", "Beefball.gluj"));
        var puck = result.Project.Entities.Single(e => e.Name == @"Entities\Puck");

        var entity = new GlueEntity { Save = puck };
        entity.BuildObjects();

        entity.Objects.Values.OfType<Circle>().ShouldNotBeEmpty();
    }

    [Fact]
    public void DoorsDemo_Player_BuildsItsSpriteAndCollisionRectangle()
    {
        var result = GlueProjectLoader.Load(Gluj("DoorsDemo", "DoorsDemo.gluj"));
        var player = result.Project.Entities.Single(e => e.Name == @"Entities\Player");

        var entity = new GlueEntity { Save = player };
        entity.BuildObjects();

        // The Sprite has no texture until Phase 4 — constructing it now is correct, not a failure.
        entity.Objects["SpriteInstance"].ShouldBeOfType<FlatRedBall2.Rendering.Sprite>();
        entity.Objects["AxisAlignedRectangleInstance"].ShouldBeOfType<AARect>();
        entity.BuildDiagnostics.ShouldNotContain(d => d.Severity == GlueDiagnosticSeverity.Error);
    }

    [Fact]
    public void DoorsDemo_StillLoadsWithZeroErrors()
    {
        var result = GlueProjectLoader.Load(Gluj("DoorsDemo", "DoorsDemo.gluj"));

        result.HasErrors.ShouldBeFalse();
        result.StartUpScreen.ShouldNotBeNull();
    }
}
