using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #772 follow-up: the folder picked via File → Open Project Folder (#770) is remembered
/// and rescanned on the next launch, so the Project tab doesn't start empty every time.
/// </summary>
public class ProjectFolderPersistenceTests
{
    private static void WriteAchx(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        var acls = new FlatRedBall2.Animation.Content.AnimationChainListSave();
        acls.Save(path);
    }

    private static void WritePng(string dir, string fileName, SKColor color)
    {
        using var bm = new SKBitmap(8, 8);
        bm.Erase(color);
        using var img = SKImage.FromBitmap(bm);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(dir, fileName), data.ToArray());
    }

    private static AppSettingsModel ReadPersistedSettings(TestServices ctx)
    {
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Assert.True(File.Exists(settingsFile.FullPath),
            $"Expected settings to be persisted at {settingsFile.FullPath}.");
        return JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(settingsFile.FullPath))!;
    }

    [AvaloniaFact]
    public async Task OpeningProjectFolder_PersistsPathWithoutWindowClose()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        WriteAchx(dir, "hero.achx");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            Dispatcher.UIThread.RunJobs();

            var settings = ReadPersistedSettings(ctx);
            Assert.Equal(dir, settings.LastProjectFolderPath);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    // Issue #839: end-to-end proof that the App/MainWindow DI wiring reaches ProjectTreeThumbnailService --
    // ProjectTreeThumbnailServiceTests and ProjectPanelControlTests already cover the generation
    // logic itself in isolation.
    [AvaloniaFact]
    public async Task OpeningProjectFolder_AchxHasAFrame_LoadsAThumbnailForItsTreeNode()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        WritePng(dir, "hero.png", SKColors.Red);
        var acls = new FlatRedBall2.Animation.Content.AnimationChainListSave();
        acls.AnimationChains.Add(new FlatRedBall2.Animation.Content.AnimationChainSave
        {
            Name = "Walk",
            Frames = { new FlatRedBall2.Animation.Content.AnimationFrameSave { TextureName = "hero.png" } },
        });
        acls.Save(Path.Combine(dir, "hero.achx"));

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            Dispatcher.UIThread.RunJobs();
            await window.ProjectPanel.ThumbnailLoadTask;

            Assert.True(window.ProjectPanel.TreeRoots[0].HasThumbnail);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    // Issue #839 follow-up: saving an edit to a tracked file must refresh its Project-tree
    // thumbnail. Before this, nothing invalidated it -- the tree kept showing the pre-edit art
    // until the folder was reopened, even after a real save to disk.
    [AvaloniaFact]
    public async Task SavingATrackedFile_RefreshesItsProjectTreeThumbnail()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        WritePng(dir, "hero.png", SKColors.Red);
        WritePng(dir, "other.png", SKColors.Blue);
        var acls = new FlatRedBall2.Animation.Content.AnimationChainListSave();
        acls.AnimationChains.Add(new FlatRedBall2.Animation.Content.AnimationChainSave
        {
            Name = "Walk",
            Frames = { new FlatRedBall2.Animation.Content.AnimationFrameSave { TextureName = "hero.png" } },
        });
        var achxPath = Path.Combine(dir, "hero.achx");
        acls.Save(achxPath);

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            Dispatcher.UIThread.RunJobs();
            await window.ProjectPanel.ThumbnailLoadTask;
            var beforeEdit = window.ProjectPanel.TreeRoots[0].Thumbnail;
            Assert.NotNull(beforeEdit);

            // Simulate reordering/resizing so the first frame now resolves to a different texture,
            // then save -- bypassing the tab-open UI flow and AppCommands' EditorProjectModelChanged
            // event (unrelated tab-sync side effects, not what's under test here) so this test
            // isolates exactly the new invalidate-then-regenerate path.
            ctx.ProjectManager.LoadAnimationChain(new AnimationEditor.Core.Paths.FilePath(achxPath));
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Frames[0].TextureName = "other.png";
            ctx.ProjectManager.SaveAnimationChainList(achxPath);
            await window.InvalidateProjectPanelThumbnailIfTrackedAsync(achxPath);

            var afterEdit = window.ProjectPanel.TreeRoots[0].Thumbnail;
            Assert.NotNull(afterEdit);
            Assert.NotSame(beforeEdit, afterEdit);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task Startup_LastProjectFolderPresent_RepopulatesProjectTab()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        WriteAchx(dir, "hero.achx");
        WriteAchx(dir, "enemy.achx");

        var ctx = TestHelpers.BuildServices();
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath,
            JsonSerializer.Serialize(new AppSettingsModel { LastProjectFolderPath = dir }));

        var window = ctx.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, window.ProjectPanel.TreeRoots.Count);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void Startup_LastProjectFolderMissing_LeavesProjectTabEmpty()
    {
        var ctx = TestHelpers.BuildServices();
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath,
            JsonSerializer.Serialize(new AppSettingsModel
            {
                LastProjectFolderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            }));

        var window = ctx.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(window.ProjectPanel.TreeRoots);
        }
        finally { window.Close(); }
    }
}
