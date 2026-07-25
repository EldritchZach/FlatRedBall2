using System.IO;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Packaging;

// Issue #504. Precompiled apos-shapes.xnb must be built with the same MonoGame MGCB line
// that template consumers resolve — not a newer preview MGCB that emits a higher MGFX version.
// Only the template copies remain (see build/AposShapesPrecompiled.props): the engine itself
// dropped its precompiled xnb once Apos.Shapes 0.7.2+ started embedding its shader in the
// assembly, but the templates still resolve a pre-0.7.2 Apos.Shapes transitively from the
// published FlatRedBall2.MonoGame/FlatRedBall2.Kni NuGet packages.
public class PrecompiledShaderCompatibilityTests
{
    private const string ExpectedMonoGameVersion = "3.8.4.1";

    [Theory]
    [InlineData("templates/frb2-desktop/build/DesktopGL/apos-shapes.xnb")]
    [InlineData("templates/frb2-multiplatform/build/DesktopGL/apos-shapes.xnb")]
    public void DesktopAposShapesXnb_MatchesPinnedMonoGameToolchain(string relativePath)
    {
        var bytes = File.ReadAllBytes(Path.Combine(RepoRoot, relativePath));
        var mgfxVersion = PrecompiledAposShapesXnbReader.GetMgfxVersion(bytes);
        var monoGameAssemblyVersion = PrecompiledAposShapesXnbReader.GetMonoGameAssemblyVersion(bytes);
        var runtimeMaxMgfx = PrecompiledAposShapesXnbReader.GetMaxMgfxVersionAcceptedByRuntime();

        monoGameAssemblyVersion.ShouldBe(
            ExpectedMonoGameVersion,
            $"Rebuild apos-shapes.xnb with MGCB {ExpectedMonoGameVersion} and recopy into {relativePath}.");

        mgfxVersion.ShouldBeLessThanOrEqualTo(
            runtimeMaxMgfx,
            customMessage: $"MGFX v{mgfxVersion} in {relativePath} exceeds MonoGame.Framework {ExpectedMonoGameVersion} (max MGFX v{runtimeMaxMgfx}).");
    }

    private static string RepoRoot => TemplatePackageReferenceTests.RepoRootForTests;
}
