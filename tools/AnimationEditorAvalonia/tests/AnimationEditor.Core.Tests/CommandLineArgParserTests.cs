using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class CommandLineArgParserTests
{
    [Fact]
    public void NullArgs_ReturnsNull()
    {
        string? result = CommandLineArgParser.ParseFileArgument(null);
        Assert.Null(result);
    }

    [Fact]
    public void EmptyArgs_ReturnsNull()
    {
        string? result = CommandLineArgParser.ParseFileArgument([]);
        Assert.Null(result);
    }

    [Fact]
    public void NoAchxArg_ReturnsNull()
    {
        string? result = CommandLineArgParser.ParseFileArgument(["--verbose", "somefile.png"]);
        Assert.Null(result);
    }

    [Fact]
    public void AchxArg_ReturnsPath()
    {
        var path = TestPaths.Abs("projects", "anim.achx");
        string? result = CommandLineArgParser.ParseFileArgument([path]);
        Assert.Equal(path, result);
    }

    [Fact]
    public void AchxArg_CaseInsensitive()
    {
        string? result = CommandLineArgParser.ParseFileArgument(["myfile.ACHX"]);
        Assert.Equal("myfile.ACHX", result);
    }

    [Fact]
    public void AchjArg_ReturnsPath()
    {
        var path = TestPaths.Abs("projects", "anim.achj");
        string? result = CommandLineArgParser.ParseFileArgument([path]);
        Assert.Equal(path, result);
    }

    [Fact]
    public void MultipleArgs_ReturnsFirstAchx()
    {
        var first  = TestPaths.Abs("first.achx");
        var second = TestPaths.Abs("second.achx");
        string? result = CommandLineArgParser.ParseFileArgument(
            ["--verbose", first, second]);
        Assert.Equal(first, result);
    }

    [Fact]
    public void NonAchxBeforeAchx_SkipsNonAchx()
    {
        string? result = CommandLineArgParser.ParseFileArgument(["--flag", "data.achx"]);
        Assert.Equal("data.achx", result);
    }

    [Fact]
    public void EmptyStringInArgs_IgnoredFindsAchx()
    {
        string? result = CommandLineArgParser.ParseFileArgument(["", "animation.achx"]);
        Assert.Equal("animation.achx", result);
    }

    [Fact]
    public void AchxPartialExtension_NotMatched()
    {
        // ".achxtra" should not match
        string? result = CommandLineArgParser.ParseFileArgument(["myfile.achxtra"]);
        Assert.Null(result);
    }
}
