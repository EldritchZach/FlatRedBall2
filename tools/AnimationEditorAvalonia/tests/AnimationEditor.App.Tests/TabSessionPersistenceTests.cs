using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression guard for issue #439: the open-tab session (<see cref="AppSettingsModel.OpenTabPaths"/>
/// / <see cref="AppSettingsModel.ActiveTabPath"/>) must be persisted as tabs change — not only from
/// the window <c>Closed</c> handler. A debugger Stop or crash kills the process without firing
/// <c>Closed</c>, so persisting only on close loses the session.
/// </summary>
public class TabSessionPersistenceTests
{
    private static string WriteAchx(string dir, string fileName, params string[] chainNames)
    {
        var path = Path.Combine(dir, fileName);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        foreach (var name in chainNames)
        {
            var chain = new AnimationChainSave { Name = name };
            chain.Frames.Add(new AnimationFrameSave { TextureName = name + ".png", FrameLength = 0.1f });
            acls.AnimationChains.Add(chain);
        }
        acls.Save(path);
        return path;
    }

    private static AppSettingsModel ReadPersistedSettings(TestServices ctx)
    {
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Assert.True(File.Exists(settingsFile.FullPath),
            $"Expected settings to be persisted at {settingsFile.FullPath} without a window Close.");
        return JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(settingsFile.FullPath))!;
    }

    /// <summary>
    /// Opening a file must persist it into <see cref="AppSettingsModel.OpenTabPaths"/> /
    /// <see cref="AppSettingsModel.ActiveTabPath"/> immediately — <b>without</b> the window
    /// ever being closed. This is the exact scenario a VS Stop / crash hits.
    /// </summary>
    [AvaloniaFact]
    public async Task OpeningFile_PersistsTabSession_WithoutWindowClose()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var path = WriteAchx(dir, "hero.achx", "Walk");

            await window.OpenFileAsTab(path);
            Dispatcher.UIThread.RunJobs();

            // No window.Close() — the session must already be on disk.
            var settings = ReadPersistedSettings(ctx);
            Assert.Contains(settings.OpenTabPaths, p => new FilePath(p) == new FilePath(path));
            Assert.Equal(new FilePath(path), new FilePath(settings.ActiveTabPath!));
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Switching the active tab between two open files must update the persisted
    /// <see cref="AppSettingsModel.ActiveTabPath"/> — again, with no window Close.
    /// </summary>
    [AvaloniaFact]
    public async Task SwitchingActiveTab_UpdatesPersistedActiveTabPath_WithoutWindowClose()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var pathA = WriteAchx(dir, "a.achx", "Walk");
            var pathB = WriteAchx(dir, "b.achx", "Run");

            await window.OpenFileAsTab(pathA);
            Dispatcher.UIThread.RunJobs();
            await window.OpenFileAsTab(pathB);
            Dispatcher.UIThread.RunJobs();

            // B is active after opening it second.
            var afterOpen = ReadPersistedSettings(ctx);
            Assert.Equal(new FilePath(pathB), new FilePath(afterOpen.ActiveTabPath!));
            Assert.Equal(2, afterOpen.OpenTabPaths.Count);

            // Re-focus A — switching the active tab must re-persist.
            await window.OpenFileAsTab(pathA);
            Dispatcher.UIThread.RunJobs();

            var afterSwitch = ReadPersistedSettings(ctx);
            Assert.Equal(new FilePath(pathA), new FilePath(afterSwitch.ActiveTabPath!));
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
