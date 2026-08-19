using AnimationEditor.App.Services;
using System.IO;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #919 — right-click Delete on a Project-tree file moves it to the OS recycle bin.
/// Only <see cref="RecycleBin"/>'s guard clauses are covered here; the actual platform move
/// (Windows <c>FileIO.FileSystem.DeleteFile</c>, macOS <c>osascript</c>, Linux trash-spec write)
/// is OS-side-effecting the same way <see cref="ShellExplorer.RevealFile"/>'s <c>Process.Start</c>
/// calls are -- deliberately left untested rather than actually touching a developer's real
/// recycle bin/trash on every test run.
/// </summary>
public class RecycleBinTests
{
    [Fact]
    public void Delete_EmptyPath_ReturnsError()
    {
        Assert.Equal("No file path was provided.", RecycleBin.Delete(string.Empty));
    }

    [Fact]
    public void Delete_FileDoesNotExist_ReturnsNotFoundError()
    {
        var missing = Path.Combine(Path.GetTempPath(), "AnimationEditorRecycleBinTests_missing.achx");

        Assert.Equal($"File not found: {missing}", RecycleBin.Delete(missing));
    }
}
