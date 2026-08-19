using Avalonia.Headless.XUnit;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #919 -- right-click Delete on a Project-tree file row. <see cref="ProjectPanelControlTests"/>
/// (Views.Tests) covers the context menu and event-raising; these tests cover MainWindow's
/// <c>DeleteProjectFileAsync</c> confirm-gate and its handoff to the <c>DeleteToRecycleBin</c> seam.
/// The seam is stubbed in every test, so the real OS recycle-bin move (<see cref="RecycleBin"/>'s
/// platform branches) is never exercised here -- see <c>RecycleBinTests</c> for the pure guard
/// clauses that are exercised without touching a real recycle bin/trash.
///
/// <c>ConfirmAsync</c> must be re-stubbed on <c>ctx.AppCommands</c> AFTER
/// <see cref="TestServices.CreateMainWindow"/>, not before: the constructor's <c>WireAppCommands</c>
/// unconditionally overwrites it with the real modal dialog (see the animation-editor-testing
/// skill's "[AvaloniaFact] is a last resort" landmine on this exact delegate). <c>ctx.AppCommands</c>
/// and MainWindow's own <c>_appCommands</c> are the same instance, so mutating it post-construction
/// still reaches the window.
/// </summary>
public class ProjectFileDeleteTests
{
    [AvaloniaFact]
    public async Task DeleteProjectFileAsync_UserDeclinesConfirm_DoesNotCallRecycleBin()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var achx = Path.Combine(dir, "hero.achx");
        File.WriteAllText(achx, "not a real achx, never read");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(false);
            string? recycledPath = null;
            window.DeleteToRecycleBin = path => { recycledPath = path; return null; };

            await window.DeleteProjectFileAsync("hero.achx");

            Assert.Null(recycledPath);
            Assert.True(File.Exists(achx));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public async Task DeleteProjectFileAsync_UserConfirms_CallsRecycleBinWithAbsolutePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var achx = Path.Combine(dir, "hero.achx");
        File.WriteAllText(achx, "not a real achx, never read");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
            string? recycledPath = null;
            window.DeleteToRecycleBin = path => { recycledPath = path; return null; };

            await window.DeleteProjectFileAsync("hero.achx");

            Assert.Equal(achx, recycledPath);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public async Task DeleteProjectFileAsync_RecycleBinReturnsError_ShowsErrorBanner()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var achx = Path.Combine(dir, "hero.achx");
        File.WriteAllText(achx, "not a real achx, never read");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
            window.DeleteToRecycleBin = _ => "Could not move to recycle bin: access denied.";

            await window.DeleteProjectFileAsync("hero.achx");

            Assert.True(window.Notifications.ErrorBanner.IsVisible);
            Assert.Contains("access denied", window.Notifications.ErrorBannerText.Text, StringComparison.Ordinal);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
