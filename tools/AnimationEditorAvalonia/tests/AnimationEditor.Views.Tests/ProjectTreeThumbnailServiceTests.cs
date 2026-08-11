using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Views.Tests;

// Issue #839: first-frame thumbnails for the Open Project Folder tree. ProjectTreeThumbnailService
// resolves a frame's texture relative to that .achx's OWN folder (via AchxFileEntry.ParentFolder),
// independent of whichever project is actually open -- these files are never loaded into
// ProjectManager.
public class ProjectTreeThumbnailServiceTests
{
    private const string AchxWithFrame =
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

    private const string AchxWithNoFrames =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <AnimationChainArraySave>
          <FileRelativeTextures>true</FileRelativeTextures>
          <TimeMeasurementUnit>Second</TimeMeasurementUnit>
          <CoordinateType>UV</CoordinateType>
          <AnimationChain>
            <Name>Empty</Name>
          </AnimationChain>
        </AnimationChainArraySave>
        """;

    private static byte[] EncodePng(int width, int height, SKColor color)
    {
        using var bm = new SKBitmap(width, height);
        bm.Erase(color);
        using var img = SKImage.FromBitmap(bm);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static AchxFileEntry MakeEntry(string achxXml, string textureName, byte[] textureBytes)
    {
        var folder = new FakeFolder("Content");
        folder.FilesByName[textureName] = new FakeFile(textureName, textureBytes);
        var achxFile = new FakeFile("hero.achx", Encoding.UTF8.GetBytes(achxXml));
        return new AchxFileEntry(achxFile, folder, "hero.achx");
    }

    [AvaloniaFact]
    public async Task GetThumbnailAsync_FrameWithSameFolderTexture_ReturnsBitmapAtRequestedSize()
    {
        var entry = MakeEntry(AchxWithFrame, "hero.png", EncodePng(64, 64, SKColors.Red));
        var svc = new ProjectTreeThumbnailService(diskCacheDirectory: null);

        var thumb = await svc.GetThumbnailAsync(entry, 28, 28);

        Assert.NotNull(thumb);
        Assert.Equal(28, thumb!.PixelSize.Width);
        Assert.Equal(28, thumb.PixelSize.Height);
    }

    [AvaloniaFact]
    public async Task GetThumbnailAsync_NoFramesInChain_ReturnsNull()
    {
        var entry = MakeEntry(AchxWithNoFrames, "hero.png", EncodePng(8, 8, SKColors.Red));
        var svc = new ProjectTreeThumbnailService(diskCacheDirectory: null);

        var thumb = await svc.GetThumbnailAsync(entry, 28, 28);

        Assert.Null(thumb);
    }

    [AvaloniaFact]
    public async Task GetThumbnailAsync_TextureNotFoundInFolder_ReturnsNull()
    {
        var entry = MakeEntry(AchxWithFrame, "other.png", EncodePng(8, 8, SKColors.Red));
        var svc = new ProjectTreeThumbnailService(diskCacheDirectory: null);

        var thumb = await svc.GetThumbnailAsync(entry, 28, 28);

        Assert.Null(thumb);
    }

    [AvaloniaFact]
    public async Task GetThumbnailAsync_CalledTwiceForSameEntry_ReturnsSameCachedInstance()
    {
        var entry = MakeEntry(AchxWithFrame, "hero.png", EncodePng(16, 16, SKColors.Blue));
        var svc = new ProjectTreeThumbnailService(diskCacheDirectory: null);

        var first = await svc.GetThumbnailAsync(entry, 28, 28);
        var second = await svc.GetThumbnailAsync(entry, 28, 28);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [AvaloniaFact]
    public async Task GetThumbnailAsync_DiskCacheConfigured_WritesAThumbnailFileToDisk()
    {
        var entry = MakeEntry(AchxWithFrame, "hero.png", EncodePng(16, 16, SKColors.Green));
        var cacheDir = Path.Combine(Path.GetTempPath(), "AnimationEditorTests", Guid.NewGuid().ToString("N"));
        var svc = new ProjectTreeThumbnailService(cacheDir);

        var thumb = await svc.GetThumbnailAsync(entry, 28, 28);

        Assert.NotNull(thumb);
        Assert.True(Directory.Exists(cacheDir) && Directory.GetFiles(cacheDir, "*.png").Length == 1,
            "Expected exactly one cached thumbnail file on disk.");
    }

    [AvaloniaFact]
    public async Task GetThumbnailAsync_DiskCacheAlreadyHasAMatchingFile_ReusesItInsteadOfRegenerating()
    {
        var entry = MakeEntry(AchxWithFrame, "hero.png", EncodePng(16, 16, SKColors.Yellow));
        var cacheDir = Path.Combine(Path.GetTempPath(), "AnimationEditorTests", Guid.NewGuid().ToString("N"));
        var first = new ProjectTreeThumbnailService(cacheDir);
        await first.GetThumbnailAsync(entry, 28, 28);

        // A fresh service instance (no in-memory cache) reading the same disk cache directory
        // must hit the cache file instead of re-parsing/re-decoding.
        var second = new ProjectTreeThumbnailService(cacheDir);
        var thumb = await second.GetThumbnailAsync(entry, 28, 28);

        Assert.NotNull(thumb);
        Assert.Single(Directory.GetFiles(cacheDir, "*.png"));
    }

    private sealed class FakeFile : IEditorFile
    {
        public FakeFile(string name, byte[] content) { Name = name; _content = content; }
        private readonly byte[] _content;
        public string Name { get; }
        public Task<Stream> OpenReadAsync() => Task.FromResult<Stream>(new MemoryStream(_content));
        public Task<Stream> OpenWriteAsync() => throw new NotSupportedException();
        public Task<FolderEntrySnapshot> GetBasicPropertiesAsync() =>
            Task.FromResult(new FolderEntrySnapshot((ulong)_content.Length, DateTimeOffset.UnixEpoch));
    }

    private sealed class FakeFolder : IEditorFolder
    {
        public FakeFolder(string name) => Name = name;
        public string Name { get; }
        public Dictionary<string, FakeFile> FilesByName { get; } = new(StringComparer.OrdinalIgnoreCase);

#pragma warning disable CS1998 // no subfolders needed for these same-folder-only tests
        public async IAsyncEnumerable<IEditorFile> GetItemsAsync()
        {
            foreach (var file in FilesByName.Values) yield return file;
        }
        public async IAsyncEnumerable<IEditorFolder> GetSubfoldersAsync() { yield break; }
#pragma warning restore CS1998

        public Task<IEditorFile?> GetFileAsync(string name) =>
            Task.FromResult(FilesByName.TryGetValue(name, out var f) ? (IEditorFile?)f : null);
    }
}
