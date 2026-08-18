using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
using Controls = AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Views.Tests;

// Issue #770: recursive .achx tree for Open Project Folder. Platform-agnostic (no Window
// dependency, unlike desktop's FilesPanelControl) -- see ProjectPanelControl's doc comment.
public class ProjectPanelControlTests
{
    [AvaloniaFact]
    public void SetEntries_DefaultExcludesBinObj()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var entries = new[]
        {
            new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx"),
            new AchxFileEntry(new FakeFile("stale.achx"), root, "bin/stale.achx"),
        };

        control.SetEntries(entries);

        Assert.Single(control.TreeRoots);
        Assert.Equal("hero.achx", control.TreeRoots[0].Name);
    }

    [AvaloniaFact]
    public void SetEntries_NoEntries_ShowsEmptyMessage()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();

        control.SetEntries(System.Array.Empty<AchxFileEntry>());

        Assert.True(control.EmptyMessage.IsVisible);
    }

    [AvaloniaFact]
    public void SelectingFileNode_RaisesFileSelected()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var entry = new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx");
        control.SetEntries(new[] { entry });
        AchxFileEntry? selected = null;
        control.FileSelected += e => selected = e;

        control.ProjectTree.SelectedItem = control.TreeRoots[0];

        Assert.Same(entry, selected);
    }

    [AvaloniaFact]
    public void UncheckExcludeBinObj_ReincludesBinObjEntries()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var entries = new[]
        {
            new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx"),
            new AchxFileEntry(new FakeFile("stale.achx"), root, "bin/stale.achx"),
        };
        control.SetEntries(entries);

        control.ExcludeBinObjCheck.IsChecked = false;

        Assert.Equal(2, control.TreeRoots.Count);
    }

    [AvaloniaFact]
    public void TypingInSearch_FiltersTreeToMatches()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var entries = new[]
        {
            new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx"),
            new AchxFileEntry(new FakeFile("enemy.achx"), root, "enemy.achx"),
        };
        control.SetEntries(entries);

        control.ProjectSearchBox.SearchBox.Text = "hero";

        Assert.Single(control.TreeRoots);
        Assert.Equal("hero.achx", control.TreeRoots[0].Name);
    }

    [AvaloniaFact]
    public void SelectingFilteredResult_ClearsSearchAndRevealsFullTreeWithSelection()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var hero = new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx");
        var enemy = new AchxFileEntry(new FakeFile("enemy.achx"), root, "enemy.achx");
        control.SetEntries(new[] { hero, enemy });
        control.ProjectSearchBox.SearchBox.Text = "hero";

        var selections = new List<AchxFileEntry>();
        control.FileSelected += e => selections.Add(e);
        control.ProjectTree.SelectedItem = control.TreeRoots[0];

        Assert.Equal([hero], selections); // fired exactly once, not re-fired by the reveal step
        Assert.False(control.ProjectSearchBox.SearchBox.IsVisible); // search collapsed
        Assert.Equal(2, control.TreeRoots.Count); // full tree restored
        Assert.Same(hero, ((AnimationEditor.Views.Controls.AchxTreeNodeVm)control.ProjectTree.SelectedItem!).Entry);
    }

    // Issue #841: real double-click (two MouseDown/MouseUp pairs, not reflection) must reach
    // FileDoubleClicked. Avalonia's TreeViewItem toggles IsExpanded from its own Tunnel-phase
    // pointer handling on the second click, so a Bubble-registered DoubleTapped handler would be
    // unreliable here -- same landmine documented for MainWindow.OnTreePointerPressed (#716).
    [AvaloniaFact]
    public void DoubleClickingFileRow_RaisesFileDoubleClicked()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var entry = new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx");
        control.SetEntries(new[] { entry });

        var window = new Window { Content = control, Width = 400, Height = 400 };
        try
        {
            window.Show();
            window.Measure(new Size(400, 400));
            window.Arrange(new Rect(0, 0, 400, 400));
            Dispatcher.UIThread.RunJobs();

            var tvi = control.ProjectTree.GetVisualDescendants().OfType<TreeViewItem>()
                .First(t => ReferenceEquals(t.DataContext, control.TreeRoots[0]));

            AchxFileEntry? doubleClicked = null;
            control.FileDoubleClicked += e => doubleClicked = e;

            var local = new Point(tvi.Bounds.Width / 2, tvi.Bounds.Height / 2);
            var p = tvi.TranslatePoint(local, window)!.Value;
            window.MouseDown(p, MouseButton.Left);
            window.MouseUp(p, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(p, MouseButton.Left);
            window.MouseUp(p, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Same(entry, doubleClicked);
        }
        finally { window.Close(); }
    }

    // Issue #841 follow-up: right-click a folder row -> "View in Explorer". Real right-click
    // (MouseDown/MouseUp, not reflection) through the pointer pipeline, same reasoning as
    // PreviewRevealInExplorerTests' RealRightClick_* tests -- ContextRequested fires off the
    // *release* event's own Handled flag, so only driving the full press+release proves routing.
    private static Window ShowInWindow(Controls.ProjectPanelControl control)
    {
        var window = new Window { Content = control, Width = 400, Height = 400 };
        window.Show();
        window.Measure(new Size(400, 400));
        window.Arrange(new Rect(0, 0, 400, 400));
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void RightClick(Window window, Controls.ProjectPanelControl control, Controls.AchxTreeNodeVm node)
    {
        var tvi = control.ProjectTree.GetVisualDescendants().OfType<TreeViewItem>()
            .First(t => ReferenceEquals(t.DataContext, node));
        // TreeViewItem.Bounds spans its own header row *plus* any expanded children, so a folder
        // with children is much taller than its 24px header (see XAML's TreeViewItem MinHeight).
        // Click near the top -- the header itself -- not the vertical centre of the whole subtree.
        var local = new Point(tvi.Bounds.Width / 2, 8);
        var p = tvi.TranslatePoint(local, window)!.Value;
        window.MouseDown(p, MouseButton.Right);
        window.MouseUp(p, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void RightClickingFolderRow_WithRevealSupported_ShowsViewInExplorerItem()
    {
        var control = new Controls.ProjectPanelControl { SupportsRevealInExplorer = true };
        var root = new FakeFolder("Content");
        control.SetEntries(new[] { new AchxFileEntry(new FakeFile("hero.achx"), root, "Sprites/hero.achx") });

        var window = ShowInWindow(control);
        try
        {
            RightClick(window, control, control.TreeRoots[0]); // "Sprites" folder

            var item = control.ProjectTree.ContextMenu!.Items.OfType<MenuItem>().Single();
            Assert.Equal("View in Explorer", item.Header);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ClickingViewInExplorer_RaisesFolderRevealRequestedWithRelativePath()
    {
        var control = new Controls.ProjectPanelControl { SupportsRevealInExplorer = true };
        var root = new FakeFolder("Content");
        control.SetEntries(new[] { new AchxFileEntry(new FakeFile("hero.achx"), root, "Sprites/Enemies/hero.achx") });

        var window = ShowInWindow(control);
        try
        {
            RightClick(window, control, control.TreeRoots[0]); // "Sprites"
            string? requested = null;
            control.FolderRevealRequested += path => requested = path;

            var item = control.ProjectTree.ContextMenu!.Items.OfType<MenuItem>().Single();
            item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal("Sprites", requested);
        }
        finally { window.Close(); }
    }

    // Issue #886: right-click a file row -> "Open Containing Folder" + "Copy Full Path", same
    // headers as the document tab strip's context menu (#881/#884) for the equivalent open file.
    [AvaloniaFact]
    public void RightClickingFileRow_WithRevealSupported_ShowsOpenFolderAndCopyPathItems()
    {
        var control = new Controls.ProjectPanelControl { SupportsRevealInExplorer = true };
        var root = new FakeFolder("Content");
        control.SetEntries(new[] { new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx") });

        var window = ShowInWindow(control);
        try
        {
            RightClick(window, control, control.TreeRoots[0]); // "hero.achx" file

            var headers = control.ProjectTree.ContextMenu!.Items.OfType<MenuItem>()
                .Select(i => i.Header).ToArray();
            Assert.Equal(new object?[] { "Open Containing Folder", "Copy Full Path" }, headers);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ClickingOpenContainingFolder_RaisesFileRevealRequestedWithRelativePath()
    {
        var control = new Controls.ProjectPanelControl { SupportsRevealInExplorer = true };
        var root = new FakeFolder("Content");
        control.SetEntries(new[] { new AchxFileEntry(new FakeFile("hero.achx"), root, "Sprites/hero.achx") });

        var window = ShowInWindow(control);
        try
        {
            RightClick(window, control, control.TreeRoots[0].Children[0]); // "hero.achx" under "Sprites"
            string? requested = null;
            control.FileRevealRequested += path => requested = path;

            var item = control.ProjectTree.ContextMenu!.Items.OfType<MenuItem>()
                .Single(i => (string)i.Header! == "Open Containing Folder");
            item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal("Sprites/hero.achx", requested);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ClickingCopyFullPath_RaisesFileCopyPathRequestedWithRelativePath()
    {
        var control = new Controls.ProjectPanelControl { SupportsRevealInExplorer = true };
        var root = new FakeFolder("Content");
        control.SetEntries(new[] { new AchxFileEntry(new FakeFile("hero.achx"), root, "Sprites/hero.achx") });

        var window = ShowInWindow(control);
        try
        {
            RightClick(window, control, control.TreeRoots[0].Children[0]); // "hero.achx" under "Sprites"
            string? requested = null;
            control.FileCopyPathRequested += path => requested = path;

            var item = control.ProjectTree.ContextMenu!.Items.OfType<MenuItem>()
                .Single(i => (string)i.Header! == "Copy Full Path");
            item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal("Sprites/hero.achx", requested);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RightClickingFileRow_WithRevealNotSupported_ShowsNoItems()
    {
        var control = new Controls.ProjectPanelControl(); // SupportsRevealInExplorer defaults false
        var root = new FakeFolder("Content");
        control.SetEntries(new[] { new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx") });

        var window = ShowInWindow(control);
        try
        {
            RightClick(window, control, control.TreeRoots[0]); // "hero.achx" file

            Assert.Empty(control.ProjectTree.ContextMenu!.Items);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RightClickingFolderRow_WithRevealNotSupported_ShowsNoItems()
    {
        var control = new Controls.ProjectPanelControl(); // SupportsRevealInExplorer defaults false
        var root = new FakeFolder("Content");
        control.SetEntries(new[] { new AchxFileEntry(new FakeFile("hero.achx"), root, "Sprites/hero.achx") });

        var window = ShowInWindow(control);
        try
        {
            RightClick(window, control, control.TreeRoots[0]); // "Sprites" folder

            Assert.Empty(control.ProjectTree.ContextMenu!.Items);
        }
        finally { window.Close(); }
    }

    // Issue #908: right-clicking blank space below the tree rows (no node under the cursor) used
    // to open a context menu with zero items. It should offer "New Animation" instead, regardless
    // of SupportsRevealInExplorer (that flag only gates filesystem-reveal items, not this).
    [AvaloniaFact]
    public void RightClickingEmptySpace_ShowsNewAnimationItem()
    {
        var control = new Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        control.SetEntries(new[] { new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx") });

        var window = ShowInWindow(control);
        try
        {
            RightClickEmptySpace(window, control);

            var item = control.ProjectTree.ContextMenu!.Items.OfType<MenuItem>().Single();
            Assert.Equal("New Animation", item.Header);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ClickingNewAnimation_RaisesNewAnimationRequested()
    {
        var control = new Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        control.SetEntries(new[] { new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx") });

        var window = ShowInWindow(control);
        try
        {
            RightClickEmptySpace(window, control);
            var raised = false;
            control.NewAnimationRequested += () => raised = true;

            var item = control.ProjectTree.ContextMenu!.Items.OfType<MenuItem>().Single();
            item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

            Assert.True(raised);
        }
        finally { window.Close(); }
    }

    private static void RightClickEmptySpace(Window window, Controls.ProjectPanelControl control)
    {
        // Below the single "hero.achx" row (~24-28px tall) but still inside the tree's bounds.
        var local = new Point(10, control.ProjectTree.Bounds.Height - 5);
        var p = control.ProjectTree.TranslatePoint(local, window)!.Value;
        window.MouseDown(p, MouseButton.Right);
        window.MouseUp(p, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();
    }

    // Issue #839: SetEntries kicks off async thumbnail generation via ProjectTreeThumbnailService.
    // ThumbnailLoadTask is the test seam for awaiting it deterministically.
    [AvaloniaFact]
    public async Task SetEntries_WithThumbnailServiceInitialized_PopulatesThumbnailOnFileNode()
    {
        const string achxWithFrame =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave>
              <FileRelativeTextures>true</FileRelativeTextures>
              <TimeMeasurementUnit>Second</TimeMeasurementUnit>
              <CoordinateType>UV</CoordinateType>
              <AnimationChain>
                <Name>Walk</Name>
                <Frame>
                  <TextureName>hero.png</TextureName>
                  <FrameLength>0.1</FrameLength>
                  <LeftCoordinate>0</LeftCoordinate>
                  <RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate>
                  <BottomCoordinate>1</BottomCoordinate>
                </Frame>
              </AnimationChain>
            </AnimationChainArraySave>
            """;
        using var textureBitmap = new SKBitmap(8, 8);
        textureBitmap.Erase(SKColors.Red);
        using var textureImage = SKImage.FromBitmap(textureBitmap);
        using var textureData = textureImage.Encode(SKEncodedImageFormat.Png, 100);

        var root = new FakeFolder("Content");
        root.Files["hero.png"] = new FakeFile("hero.png", textureData.ToArray());
        var achxFile = new FakeFile("hero.achx", Encoding.UTF8.GetBytes(achxWithFrame));
        var entry = new AchxFileEntry(achxFile, root, "hero.achx");

        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        control.Initialize(new ProjectTreeThumbnailService(diskCacheDirectory: null));

        control.SetEntries(new[] { entry });
        await control.ThumbnailLoadTask;

        Assert.True(control.TreeRoots[0].HasThumbnail);
    }

    private sealed class FakeFile : IEditorFile
    {
        public FakeFile(string name, byte[]? content = null) { Name = name; _content = content; }
        private readonly byte[]? _content;
        public string Name { get; }
        public Task<Stream> OpenReadAsync() => _content is null
            ? throw new NotSupportedException()
            : Task.FromResult<Stream>(new MemoryStream(_content));
        public Task<Stream> OpenWriteAsync() => throw new NotSupportedException();
        public Task<FolderEntrySnapshot> GetBasicPropertiesAsync() =>
            Task.FromResult(new FolderEntrySnapshot(null, null));
    }

    private sealed class FakeFolder : IEditorFolder
    {
        public FakeFolder(string name) => Name = name;
        public string Name { get; }
        public Dictionary<string, FakeFile> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
#pragma warning disable CS1998 // no subfolders/items to enumerate -- these entries are hand-built for the tree tests above
        public async IAsyncEnumerable<IEditorFile> GetItemsAsync() { yield break; }
        public async IAsyncEnumerable<IEditorFolder> GetSubfoldersAsync() { yield break; }
#pragma warning restore CS1998
        public Task<IEditorFile?> GetFileAsync(string name) =>
            Task.FromResult(Files.TryGetValue(name, out var f) ? (IEditorFile?)f : null);
    }
}
