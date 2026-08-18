using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #910: selecting a file in the Project list opens/activates its tab, but switching tabs
/// the other way (via the tab strip) never updated the Project list's own selection to match --
/// the two were only wired one direction.
/// </summary>
public class ProjectPanelTabSwitchSyncTests
{
    private static void WriteAchx(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        var acls = new FlatRedBall2.Animation.Content.AnimationChainListSave
        {
            CoordinateType = FlatRedBall2.Animation.Content.TextureCoordinateType.Pixel,
        };
        acls.Save(path);
    }

    [AvaloniaFact]
    public async Task ActivatingTabViaTabStrip_UpdatesProjectListSelectionToMatch()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var pathA = Path.Combine(dir, "a.achx");
            var pathB = Path.Combine(dir, "b.achx");
            WriteAchx(dir, "a.achx");
            WriteAchx(dir, "b.achx");

            await window.OpenProjectFolderForTestAsync(dir);
            Dispatcher.UIThread.RunJobs();
            await window.ProjectPanel.ThumbnailLoadTask;

            // Open both as permanent tabs via File > Open, bypassing the Project list entirely --
            // its selection should still start out unset since neither open went through it.
            await window.OpenFileAsTab(pathA);
            Dispatcher.UIThread.RunJobs();
            await window.OpenFileAsTab(pathB);
            Dispatcher.UIThread.RunJobs();

            var tabManager = (TabManager)typeof(MainWindow)
                .GetField("_tabManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(window)!;
            var tabA = tabManager.Tabs.First(t => t.Path == new FilePath(pathA));

            // Simulate the user clicking tab A in the tab strip (its PointerReleased handler
            // calls this same method -- see MainWindow.axaml.cs WireTabBar).
            var activateMethod = typeof(MainWindow)
                .GetMethod("ActivateTabAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            await (Task)activateMethod.Invoke(window, [tabA])!;
            Dispatcher.UIThread.RunJobs();

            Assert.NotNull(window.ProjectPanel.SelectedEntry);
            Assert.Equal(new FilePath(pathA),
                new FilePath(((AnimationEditor.App.Services.DiskEditorFile)window.ProjectPanel.SelectedEntry!.File).FullPath));
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
