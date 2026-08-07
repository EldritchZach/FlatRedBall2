using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers the last hop of Phases 11 and 12: an entity whose authored physics are loaded still needs
// something telling it to move. Glue records that as a single InputDevice choice per entity.
public class GlueInputTests
{
    private static GlueProject LoadDoorsDemo() =>
        GlueProject.Load(Path.Combine(
            AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo", "DoorsDemo.gluj"));

    private static EntitySave EntityWithInputDevice(int inputDevice)
    {
        var save = new EntitySave { Name = @"Entities\Test" };
        save.Properties.Add(new PropertySave
        {
            Name = "InputDevice",
            Value = System.Text.Json.JsonDocument.Parse(inputDevice.ToString()).RootElement.Clone(),
        });
        return save;
    }

    // Glue's enum, from EntityInputMovementPlugin's MainViewModel: the default is 0, and it is not
    // "none" -- an entity that says nothing still gets input.
    [Fact]
    public void InputDevice_TheAbsentDefault_IsGamepadWithKeyboardFallback()
    {
        new EntitySave().InputDevice.ShouldBe((int)GlueInputDevice.GamepadWithKeyboardFallback);
        ((int)GlueInputDevice.GamepadWithKeyboardFallback).ShouldBe(0);
        ((int)GlueInputDevice.None).ShouldBe(1);
        ((int)GlueInputDevice.ZeroInputDevice).ShouldBe(2);
    }

    [Fact]
    public void Bind_GamepadWithKeyboardFallback_ProducesMovementAndJumpInput()
    {
        var engine = new FlatRedBallService();

        var bound = GlueInputBinder.Bind(
            EntityWithInputDevice((int)GlueInputDevice.GamepadWithKeyboardFallback), engine.Input);

        bound.MovementInput.ShouldNotBeNull();
        bound.JumpInput.ShouldNotBeNull();
    }

    // "None" means the game wires its own input -- Glue generates nothing at all for it, so binding
    // anything would override a choice the author made deliberately.
    [Fact]
    public void Bind_None_LeavesInputUnbound()
    {
        var engine = new FlatRedBallService();

        var bound = GlueInputBinder.Bind(
            EntityWithInputDevice((int)GlueInputDevice.None), engine.Input);

        bound.MovementInput.ShouldBeNull();
        bound.JumpInput.ShouldBeNull();
    }

    // ZeroInputDevice is not the same as None: it binds a real input that always reads zero, which
    // is how an author pins an entity still without leaving it open to a later binding.
    [Fact]
    public void Bind_ZeroInputDevice_BindsAnInputThatAlwaysReadsZero()
    {
        var engine = new FlatRedBallService();

        var bound = GlueInputBinder.Bind(
            EntityWithInputDevice((int)GlueInputDevice.ZeroInputDevice), engine.Input);

        bound.MovementInput.ShouldNotBeNull();
        bound.MovementInput!.X.ShouldBe(0f);
        bound.MovementInput.Y.ShouldBe(0f);
        bound.JumpInput!.IsDown.ShouldBeFalse();
    }

    [Fact]
    public void BuildObjects_APlatformerEntity_ReceivesItsInput()
    {
        var engine = new FlatRedBallService();
        var project = LoadDoorsDemo();
        engine.GlueProject = project;
        engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });

        var player = project.CreateEntity(@"Entities\Player", engine.CurrentScreen);

        // The Player's authored physics reached the behaviour in Phase 12; this is what finally
        // gives it a way to be told to move.
        player.Platformer.MovementInput.ShouldNotBeNull();
        player.Platformer.JumpInput.ShouldNotBeNull();
    }

    // Phase 11's deferred pieces: the values have to reach TopDownBehavior, and Glue's generated code
    // defaults to the first CSV row rather than leaving the entity with no values at all.
    //
    // The name lives in the dictionary key, not on TopDownValues -- which is why the reader returns
    // a dictionary and the first-row default is the first *entry*.
    private static Dictionary<string, TopDownValues> TwoMovements() => new()
    {
        ["Default"] = new TopDownValues { MaxSpeed = 100f },
        ["Fast"] = new TopDownValues { MaxSpeed = 300f },
    };

    [Fact]
    public void TopDown_AnEntityWithMovementValues_UsesTheFirstRowByDefault()
    {
        var entity = new GlueEntity();

        GlueMovementValues.ApplyTopDown(entity, TwoMovements());

        entity.TopDown.MovementValues.ShouldNotBeNull();
        entity.TopDown.MovementValues!.MaxSpeed.ShouldBe(100f);
    }

    [Fact]
    public void TopDown_NamedValues_CanBeSelectedByName()
    {
        var entity = new GlueEntity();
        GlueMovementValues.ApplyTopDown(entity, TwoMovements());

        entity.SetTopDownMovement("Fast");

        entity.TopDown.MovementValues!.MaxSpeed.ShouldBe(300f);
    }

    // A data-driven name has no compiler checking it, and a typo that stopped the entity dead would
    // read as a movement bug rather than a bad string.
    [Fact]
    public void TopDown_AnUnknownMovementName_LeavesTheCurrentValuesAlone()
    {
        var entity = new GlueEntity();
        GlueMovementValues.ApplyTopDown(entity, TwoMovements());

        entity.SetTopDownMovement("NoSuchMovement");

        entity.TopDown.MovementValues!.MaxSpeed.ShouldBe(100f);
    }

    [Fact]
    public void TopDown_SelectionIsCaseInsensitive()
    {
        var entity = new GlueEntity();
        GlueMovementValues.ApplyTopDown(entity, TwoMovements());

        entity.SetTopDownMovement("fast");

        entity.TopDown.MovementValues!.MaxSpeed.ShouldBe(300f);
    }
}
