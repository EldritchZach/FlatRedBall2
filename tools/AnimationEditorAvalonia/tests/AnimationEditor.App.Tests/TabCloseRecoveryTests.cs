using System.Linq;
using System.Reflection;
using AnimationEditor.Core.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #927: closing an untitled tab left the crash-recovery file on disk, so the next
/// launch offered to restore content the user had just explicitly discarded by closing the
/// tab. Autosave-on-edit (<c>AppCommands.SaveCurrentAnimationChainList</c>) writes the
/// recovery file on every edit of an untitled document; closing that tab must clear it.
/// </summary>
public class TabCloseRecoveryTests
{
    private static TabManager GetTabManager(MainWindow window) =>
        (TabManager)typeof(MainWindow)
            .GetField("_tabManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    private static void CloseTab(MainWindow window, TabEntry tab) =>
        typeof(MainWindow)
            .GetMethod("CloseTab", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, [tab]);

    [AvaloniaFact]
    public void ClosingUntitledTabWithRecoveryFile_DeletesRecoveryFile()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            // File -> New opens a fresh untitled tab.
            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            // Editing an untitled document autosaves to the recovery file (FileName is null).
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();
            Assert.True(ctx.IoManager.RecoveryFileExists());

            var tabManager = GetTabManager(window);
            var tab = tabManager.Tabs.Single();

            CloseTab(window, tab);
            Dispatcher.UIThread.RunJobs();

            Assert.False(ctx.IoManager.RecoveryFileExists());
        }
        finally { window.Close(); }
    }
}
