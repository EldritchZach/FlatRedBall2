using AnimationEditor.App.Services;
using AnimationEditor.Browser;
using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Views.Tests;

// #889: BrowserProjectLoader.TryLoadAsync/TryLoadFromNamedAchxAsync call
// AnimationChainListSave.FromString, which content-sniffs XML vs. JSON -- neither method has its
// own achx/achj branch to get wrong. Characterizes both dialects through both entry points, plus
// the root-relative (not bare-filename) texture matching TryLoadAsync depends on. This class had
// no test coverage at all before #889 (no test project referenced AnimationEditor.Browser, which
// targets net10.0-browser and can't be referenced from a desktop xunit project -- NU1201). Moved
// here (Views project, same "AnimationEditor.Browser" namespace, zero call-site change) because it
// has no actual browser/WASM dependency, same rationale as the #535 M2 Views split.
public class BrowserProjectLoaderTests
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

    private static byte[] EncodePng(int width, int height, SKColor color)
    {
        using var bm = new SKBitmap(width, height);
        bm.Erase(color);
        using var img = SKImage.FromBitmap(bm);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string JsonAchWithFrame(string textureName) =>
        BuildAcls(textureName).ToJsonString();

    private static AnimationChainListSave BuildAcls(string textureName)
    {
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = textureName,
            FrameLength = 0.1f,
            LeftCoordinate = 0,
            RightCoordinate = 1,
            TopCoordinate = 0,
            BottomCoordinate = 1,
        });
        acls.AnimationChains.Add(chain);
        return acls;
    }

    private static (ProjectManager Pm, ThumbnailService Thumbnails, ISelectedState Selected) MakeContext()
    {
        var pm = new ProjectManager();
        var thumbnails = new ThumbnailService(pm);
        var selected = new SelectedState(pm);
        return (pm, thumbnails, selected);
    }

    // ── TryLoadAsync ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TryLoadAsync_AchxFile_ParsesXmlAndLoadsIntoProjectManager()
    {
        var achxFile = new FakeFile("hero.achx", Encoding.UTF8.GetBytes(AchxWithFrame));
        var pngFile = new FakeFile("hero.png", EncodePng(16, 16, SKColors.Red));
        var files = new List<IEditorFile> { achxFile, pngFile };
        var (pm, thumbnails, selected) = MakeContext();

        var result = await BrowserProjectLoader.TryLoadAsync(files, pm, thumbnails, selected);

        Assert.Same(achxFile, result);
        Assert.Equal("Walk", pm.AnimationChainListSave!.AnimationChains.Single().Name);
        Assert.Equal("Walk", selected.SelectedChain!.Name);
    }

    [Fact]
    public async Task TryLoadAsync_AchjFile_ParsesJsonAndLoadsIntoProjectManager()
    {
        var achjFile = new FakeFile("hero.achj", Encoding.UTF8.GetBytes(JsonAchWithFrame("hero.png")));
        var pngFile = new FakeFile("hero.png", EncodePng(16, 16, SKColors.Blue));
        var files = new List<IEditorFile> { achjFile, pngFile };
        var (pm, thumbnails, selected) = MakeContext();

        var result = await BrowserProjectLoader.TryLoadAsync(files, pm, thumbnails, selected);

        Assert.Same(achjFile, result);
        Assert.Equal("Walk", pm.AnimationChainListSave!.AnimationChains.Single().Name);
        Assert.Equal("Walk", selected.SelectedChain!.Name);
    }

    [Fact]
    public async Task TryLoadAsync_NoAchxOrAchjAmongFiles_ReturnsNullAndLeavesProjectManagerEmpty()
    {
        var files = new List<IEditorFile> { new FakeFile("hero.png", EncodePng(4, 4, SKColors.Red)) };
        var (pm, thumbnails, selected) = MakeContext();

        var result = await BrowserProjectLoader.TryLoadAsync(files, pm, thumbnails, selected);

        Assert.Null(result);
        Assert.Null(pm.AnimationChainListSave);
    }

    [Fact]
    public async Task TryLoadAsync_MatchingTexture_SeedsThumbnailServiceByTextureName()
    {
        var achxFile = new FakeFile("hero.achx", Encoding.UTF8.GetBytes(AchxWithFrame));
        var pngFile = new FakeFile("hero.png", EncodePng(32, 24, SKColors.Green));
        var files = new List<IEditorFile> { achxFile, pngFile };
        var (pm, thumbnails, selected) = MakeContext();

        await BrowserProjectLoader.TryLoadAsync(files, pm, thumbnails, selected);

        var bitmap = thumbnails.GetBitmap("hero.png");
        Assert.NotNull(bitmap);
        Assert.Equal(32, bitmap!.Width);
        Assert.Equal(24, bitmap.Height);
    }

    [Fact]
    public async Task TryLoadAsync_TextureNotAmongFiles_LoadsProjectAnywayWithoutThrowing()
    {
        var achxFile = new FakeFile("hero.achx", Encoding.UTF8.GetBytes(AchxWithFrame));
        var files = new List<IEditorFile> { achxFile };
        var (pm, thumbnails, selected) = MakeContext();

        var result = await BrowserProjectLoader.TryLoadAsync(files, pm, thumbnails, selected);

        Assert.Same(achxFile, result);
        Assert.Equal("Walk", pm.AnimationChainListSave!.AnimationChains.Single().Name);
    }

    // Root-relative matching, not bare-filename: a texture in the achx's own subfolder must match
    // by full "Dir/name.png" path, and a same-named PNG living in a different folder must not.
    [Fact]
    public async Task TryLoadAsync_TextureInAchxOwnSubfolder_MatchesByRootRelativePathNotBareName()
    {
        var achxFile = new FakeFile("Project/hero.achx", Encoding.UTF8.GetBytes(AchxWithFrame));
        var correctPng = new FakeFile("Project/hero.png", EncodePng(16, 16, SKColors.Red));
        var decoyPng = new FakeFile("Other/hero.png", EncodePng(8, 8, SKColors.Yellow));
        var files = new List<IEditorFile> { achxFile, correctPng, decoyPng };
        var (pm, thumbnails, selected) = MakeContext();

        await BrowserProjectLoader.TryLoadAsync(files, pm, thumbnails, selected);

        var bitmap = thumbnails.GetBitmap("hero.png");
        Assert.NotNull(bitmap);
        Assert.Equal(16, bitmap!.Width); // matched Project/hero.png, not the 8x8 decoy
    }

    // ── TryLoadFromNamedAchxAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task TryLoadFromNamedAchxAsync_AchxFile_ParsesXmlAndLoadsIntoProjectManager()
    {
        var achxFile = new FakeFile("hero.achx", Encoding.UTF8.GetBytes(AchxWithFrame));
        var pngFile = new FakeFile("hero.png", EncodePng(16, 16, SKColors.Red));
        var (pm, thumbnails, selected) = MakeContext();

        var result = await BrowserProjectLoader.TryLoadFromNamedAchxAsync(
            achxFile, name => Task.FromResult<IEditorFile?>(name == "hero.png" ? pngFile : null),
            pm, thumbnails, selected);

        Assert.Same(achxFile, result);
        Assert.Equal("Walk", pm.AnimationChainListSave!.AnimationChains.Single().Name);
        Assert.Equal("Walk", selected.SelectedChain!.Name);
    }

    [Fact]
    public async Task TryLoadFromNamedAchxAsync_AchjFile_ParsesJsonAndLoadsIntoProjectManager()
    {
        var achjFile = new FakeFile("hero.achj", Encoding.UTF8.GetBytes(JsonAchWithFrame("hero.png")));
        var pngFile = new FakeFile("hero.png", EncodePng(16, 16, SKColors.Blue));
        var (pm, thumbnails, selected) = MakeContext();

        var result = await BrowserProjectLoader.TryLoadFromNamedAchxAsync(
            achjFile, name => Task.FromResult<IEditorFile?>(name == "hero.png" ? pngFile : null),
            pm, thumbnails, selected);

        Assert.Same(achjFile, result);
        Assert.Equal("Walk", pm.AnimationChainListSave!.AnimationChains.Single().Name);
        Assert.Equal("Walk", selected.SelectedChain!.Name);
    }

    [Fact]
    public async Task TryLoadFromNamedAchxAsync_ResolverReturnsNull_LoadsProjectAnywayWithoutThrowing()
    {
        var achxFile = new FakeFile("hero.achx", Encoding.UTF8.GetBytes(AchxWithFrame));
        var (pm, thumbnails, selected) = MakeContext();

        var result = await BrowserProjectLoader.TryLoadFromNamedAchxAsync(
            achxFile, _ => Task.FromResult<IEditorFile?>(null), pm, thumbnails, selected);

        Assert.Same(achxFile, result);
        Assert.Equal("Walk", pm.AnimationChainListSave!.AnimationChains.Single().Name);
    }

    private sealed class FakeFile : IEditorFile
    {
        public FakeFile(string name, byte[] content) { Name = name; Content = content; }
        public byte[] Content { get; }
        public string Name { get; }
        public Task<Stream> OpenReadAsync() => Task.FromResult<Stream>(new MemoryStream(Content));
        public Task<Stream> OpenWriteAsync() => throw new NotSupportedException();
        public Task<FolderEntrySnapshot> GetBasicPropertiesAsync() =>
            Task.FromResult(new FolderEntrySnapshot((ulong)Content.Length, DateTimeOffset.UnixEpoch));
    }
}
