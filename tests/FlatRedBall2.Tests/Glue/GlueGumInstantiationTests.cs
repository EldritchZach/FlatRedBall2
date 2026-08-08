using System;
using System.IO;
using System.Linq;
using FlatRedBall2.Glue;
using Gum.Managers;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

/// <summary>
/// One engine, initialized from a Glue project, shared by the whole class.
/// </summary>
/// <remarks>
/// Gum initializes once per process — a second <c>Initialize</c> throws — so every test here has to
/// share one service rather than building its own. That also makes this fixture the only place the
/// Glue-project boot path runs, which is what the tests are really about.
/// </remarks>
public sealed class GlueGumFixture : IDisposable
{
    public GlueGumFixture()
    {
        StageFixtureContent();

        try
        {
            _game = new Game();
            _ = new GraphicsDeviceManager(_game)
            {
                PreferredBackBufferWidth = 64,
                PreferredBackBufferHeight = 64,
            };
            _game.RunOneFrame();
        }
        catch (Exception e)
        {
            // No display, no driver, or a headless agent — same contract as GraphicsDeviceFixture.
            System.Diagnostics.Debug.WriteLine($"[tests] No graphics device available: {e.Message}");
            _game?.Dispose();
            _game = null;
            return;
        }

        Service = new FlatRedBallService();
        Service.Initialize(_game, new EngineInitSettings
        {
            GlueProjectFile = Path.Combine("Glue", "Fixtures", "DoorsDemo", "DoorsDemo.gluj"),
        });
    }

    private Game? _game;

    /// <summary>The engine, or null when this machine cannot provide a device.</summary>
    public FlatRedBallService? Service { get; }

    public bool IsAvailable => Service is not null;

    public static string FixtureDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo");

    /// <summary>
    /// Stages the fixture's Gum project next to the test binary, because Gum's FileManager resolves
    /// every path against the app's own Content folder rather than the Glue project's. A real game
    /// needs no equivalent: its Content folder *is* the Glue project's.
    /// </summary>
    private static void StageFixtureContent()
    {
        CopyDirectory(
            Path.Combine(FixtureDirectory, "Content", "GumProject"),
            Path.Combine(AppContext.BaseDirectory, "Content", "GumProject"));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    public void Dispose() => _game?.Dispose();
}

// Covers actually showing a loaded project's UI, which needs a Gum runtime and so a real device.
// In GraphicsDeviceCollection so this does not run in parallel with anything else that builds a
// Game — that combination fails intermittently.
[Collection(GraphicsDeviceCollection.Name)]
public class GlueGumInstantiationTests
{
    private readonly GlueGumFixture _fixture;

    public GlueGumInstantiationTests(GlueGumFixture fixture) => _fixture = fixture;

    [Fact]
    public void Initialize_WithAGlueProject_LoadsTheGumProjectItReferences()
    {
        if (!_fixture.IsAvailable)
            return;

        // Gum only works with a project it loaded itself (G57), so the proof is that Gum's own
        // finder has it — not merely that the loader found the path.
        ObjectFinder.Self.GumProjectSave.ShouldNotBeNull();
        ObjectFinder.Self.GumProjectSave!.Screens.ShouldContain(s => s.Name == "GameScreenGum");
    }

    [Fact]
    public void Initialize_WithAGlueProject_ExposesItSoTheCallerNeedNotLoadItTwice()
    {
        if (!_fixture.IsAvailable)
            return;

        var service = _fixture.Service!;

        service.GlueProject.ShouldNotBeNull();
        service.GlueProject!.StartUpScreen!.Name.ShouldBe(@"Screens\Level1");
        service.GlueProject.Result.GumProjectFile.ShouldBe("GumProject/GumProject.gumx");
        // Content comes wired to the engine's loader, so assets resolve without the caller building
        // a source by hand.
        service.GlueProject.Content.ShouldNotBeNull();
    }

    [Fact]
    public void Start_AGlueScreenDeclaringAGumScreen_ShowsIt()
    {
        if (!_fixture.IsAvailable)
            return;

        var service = _fixture.Service!;
        var gameScreen = service.GlueProject!.Result.Project.Screens
            .Single(s => s.Name == @"Screens\GameScreen");

        service.Start<GlueScreen>(screen =>
        {
            screen.Save = gameScreen;
            screen.Project = service.GlueProject;
        });

        var current = (GlueScreen)service.CurrentScreen;
        current.GumScreen.ShouldNotBeNull();
        current.GumScreen!.Children.ShouldNotBeEmpty();
    }

    // G53 — Level1 is the project's StartUpScreen and declares no .gusx of its own, so the
    // inheriting case is the primary boot path rather than an edge case.
    [Fact]
    public void Start_TheStartUpScreen_ShowsTheGumScreenItInherits()
    {
        if (!_fixture.IsAvailable)
            return;

        var service = _fixture.Service!;

        service.Start<GlueScreen>(screen =>
        {
            screen.Save = service.GlueProject!.StartUpScreen;
            screen.Project = service.GlueProject;
        });

        var current = (GlueScreen)service.CurrentScreen;
        current.GlueName.ShouldBe(@"Screens\Level1");
        current.GumScreen.ShouldNotBeNull();
    }
}
