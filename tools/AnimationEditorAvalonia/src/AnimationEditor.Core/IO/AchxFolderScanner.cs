using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Recursively discovers every <c>.achx</c> under an <see cref="IEditorFolder"/> (issue #770).
/// Operates entirely through <see cref="IEditorFolder"/>/<see cref="IEditorFile"/> so the exact
/// same scan drives both desktop's <c>System.IO</c> adapter and the browser's native-handle
/// adapter. Does not itself exclude <c>bin</c>/<c>obj</c> — that's <see cref="BinObjPathFilter"/>,
/// applied by the caller (e.g. at tree-build time) so toggling the exclusion checkbox doesn't
/// require a re-scan.
/// </summary>
public static class AchxFolderScanner
{
    public static async Task<IReadOnlyList<AchxFileEntry>> ScanAsync(IEditorFolder rootFolder)
    {
        var results = new List<AchxFileEntry>();
        await ScanAsync(rootFolder, relativePrefix: "", results);
        return results;
    }

    /// <summary>
    /// True when <paramref name="path"/> — an absolute path, a relative path, or a bare file
    /// name — has a <c>.achx</c> (XML) or <c>.achj</c> (JSON) extension. Shared by the recursive
    /// scan above and by <see cref="FolderWatcher"/>'s live Project-tree watch (#843), so both
    /// agree on what counts as an animation chain file.
    /// </summary>
    public static bool IsAchxPath(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".achx", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".achj", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ScanAsync(
        IEditorFolder folder, string relativePrefix, List<AchxFileEntry> results)
    {
        await foreach (var file in folder.GetItemsAsync())
        {
            if (!IsAchxPath(file.Name))
                continue;

            results.Add(new AchxFileEntry(file, folder, CombineRelativePath(relativePrefix, file.Name)));
        }

        await foreach (var subfolder in folder.GetSubfoldersAsync())
        {
            var subPrefix = CombineRelativePath(relativePrefix, subfolder.Name);
            await ScanAsync(subfolder, subPrefix, results);
        }
    }

    private static string CombineRelativePath(string prefix, string name) =>
        prefix.Length == 0 ? name : prefix + "/" + name;
}

/// <summary>
/// One discovered <c>.achx</c>: its file handle, the <see cref="IEditorFolder"/> it was found
/// directly inside (needed on the web to resolve its sibling textures — see
/// <c>BrowserProjectLoader.TryLoadAsync</c>), and its path relative to the scanned root.
/// </summary>
public sealed record AchxFileEntry(IEditorFile File, IEditorFolder ParentFolder, string RelativePath)
{
    public string FileName => File.Name;
}
