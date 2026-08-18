using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #904: the preview pane's dragged height must survive a restart instead of always
/// resetting to the fixed 250px default.
/// </summary>
public class PreviewPaneHeightPersistenceTests
{
    [AvaloniaFact]
    public void Startup_PersistedPreviewPaneHeight_AppliesToAchxEditorPaneRow()
    {
        var ctx = TestHelpers.BuildServices();
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath,
            JsonSerializer.Serialize(new AppSettingsModel { PreviewPaneHeight = 420.0 }));

        var window = ctx.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(420.0, window.AchxEditorPane.RowDefinitions[3].Height.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Closing_AfterResizingPreviewPane_PersistsRowHeight()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Stand in for a real GridSplitter drag, which just changes the row's Height.
        window.AchxEditorPane.RowDefinitions[3].Height = new GridLength(410.0, GridUnitType.Pixel);
        window.Close();

        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        var settings = JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(settingsFile.FullPath))!;
        Assert.Equal(410.0, settings.PreviewPaneHeight);
    }
}
