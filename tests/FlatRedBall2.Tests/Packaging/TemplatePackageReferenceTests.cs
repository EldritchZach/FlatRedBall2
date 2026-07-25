using System;
using System.IO;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Packaging;

public class TemplatePackageReferenceTests
{
    [Theory]
    [InlineData("templates/frb2-desktop/MyGame.Desktop/MyGame.Desktop.csproj")]
    [InlineData("templates/frb2-multiplatform/MyGame.Desktop/MyGame.Desktop.csproj")]
    public void DesktopTemplate_DoesNotPinAposShapes(string relativeCsprojPath)
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot, relativeCsprojPath));

        Regex.IsMatch(csproj, @"<PackageReference\s+Include=""Apos\.Shapes""")
            .ShouldBeFalse(
                "Desktop templates must not pin Apos.Shapes; version flows transitively from FlatRedBall2.MonoGame.");
    }

    [Fact]
    public void BlazorGLTemplate_DoesNotPinAposShapesKni()
    {
        var csproj = File.ReadAllText(
            Path.Combine(RepoRoot, "templates/frb2-multiplatform/MyGame.BlazorGL/MyGame.BlazorGL.csproj"));

        Regex.IsMatch(csproj, @"<PackageReference\s+Include=""Apos\.Shapes\.KNI""")
            .ShouldBeFalse(
                "BlazorGL templates must not pin Apos.Shapes.KNI; version flows transitively from FlatRedBall2.Kni.");
    }

    private static string RepoRoot => RepoRootForTests;

    internal static string RepoRootForTests
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "src", "FlatRedBall2.csproj")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("Could not locate repository root.");
        }
    }
}
