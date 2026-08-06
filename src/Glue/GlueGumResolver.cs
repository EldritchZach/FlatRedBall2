using System;
using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Finds a Glue project's Gum project, and turns its <c>.gusx</c>/<c>.gucx</c> references into the
/// element names Gum looks elements up by.
/// </summary>
/// <remarks>
/// Resolution only — nothing here touches Gum. Instantiating the visual needs a Gum runtime that
/// <see cref="FlatRedBallService"/> has initialized, so it lives at the point of use.
/// </remarks>
public static class GlueGumResolver
{
    private const string GumProjectExtension = ".gumx";
    private const string GumProjectJsonExtension = ".gumj";

    private static readonly string[] GumElementExtensions =
    {
        ".gusx", ".gucx", ".gusj", ".gucj",
    };

    /// <summary>
    /// The Gum project in <paramref name="project"/>'s global files, as authored — a path relative
    /// to the project's <c>Content</c> folder. Null when the project has no Gum project.
    /// </summary>
    public static string? FindGumProjectFile(GlueProjectSave project) =>
        project.GlobalFiles.FirstOrDefault(IsGumProject)?.Name;

    /// <summary>Whether this global file is the project's Gum project.</summary>
    public static bool IsGumProject(ReferencedFileSave file) =>
        HasExtension(file.Name, GumProjectExtension) || HasExtension(file.Name, GumProjectJsonExtension);

    /// <summary>Whether this referenced file is a Gum screen or component.</summary>
    public static bool IsGumElement(ReferencedFileSave file) =>
        GumElementExtensions.Any(extension => HasExtension(file.Name, extension));

    /// <summary>
    /// Whether this file loads through FRB1's legacy <c>GumIdb</c> model, which reads the
    /// <c>.gusx</c> directly rather than looking the element up in the project.
    /// </summary>
    /// <remarks>
    /// A genuinely different loading model, not just a different type name — worth a diagnostic
    /// rather than silent best-effort handling. See G54.
    /// </remarks>
    public static bool IsLegacyGumIdb(ReferencedFileSave file) =>
        file.RuntimeType is "FlatRedBall.Gum.GumIdb";

    /// <summary>
    /// The Gum element name for a referenced <c>.gusx</c>/<c>.gucx</c>, which is its path minus the
    /// Gum project's own folder, the <c>Screens</c>/<c>Components</c> category folder, and the
    /// extension. <c>GumProject/Screens/GameScreenGum.gusx</c> becomes <c>GameScreenGum</c>.
    /// </summary>
    /// <remarks>
    /// Folders <em>below</em> the category survive: Gum knows a nested component as
    /// <c>Controls/ButtonStandard</c>, so stripping them would break the lookup.
    /// </remarks>
    /// <param name="referencedFileName">The referenced file's <see cref="ReferencedFileSave.Name"/>.</param>
    /// <param name="gumProjectFile">The project's Gum project path, whose folder comes off the front.</param>
    public static string? ElementNameFor(string? referencedFileName, string? gumProjectFile)
    {
        if (string.IsNullOrWhiteSpace(referencedFileName))
            return null;

        // Documented as forward-slashed, written both ways in practice.
        string path = referencedFileName!.Replace('\\', '/');

        string? gumProjectFolder = DirectoryOf(gumProjectFile?.Replace('\\', '/'));
        if (!string.IsNullOrEmpty(gumProjectFolder))
            path = StripLeadingSegment(path, gumProjectFolder!);

        path = StripLeadingSegment(path, "Screens");
        path = StripLeadingSegment(path, "Components");

        int lastDot = path.LastIndexOf('.');
        int lastSlash = path.LastIndexOf('/');
        if (lastDot > lastSlash)
            path = path.Substring(0, lastDot);

        return path.Length == 0 ? null : path;
    }

    /// <summary>
    /// The Gum screen <paramref name="element"/> shows, or null when neither it nor anything it
    /// derives from references one.
    /// </summary>
    /// <remarks>
    /// A loaded project has already merged each element with its base (<see cref="GlueProjectLoader"/>
    /// flattens on load), so the inherited case normally resolves from <paramref name="element"/>'s
    /// own referenced files. The walk up <paramref name="project"/> is the fallback for an element
    /// that has not been flattened.
    /// </remarks>
    public static string? GumElementNameFor(
        GlueElement element, GlueProjectSave project, string? gumProjectFile)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        GlueElement? current = element;

        while (current is not null)
        {
            var gumFile = current.ReferencedFiles.FirstOrDefault(IsGumElement);
            if (gumFile is not null)
                return ElementNameFor(gumFile.Name, gumProjectFile);

            string? baseName = current.BaseElement;
            if (string.IsNullOrEmpty(baseName) || !seen.Add(baseName!))
                return null;

            current = FindElement(project, baseName!);
        }

        return null;
    }

    private static GlueElement? FindElement(GlueProjectSave project, string name)
    {
        foreach (var screen in project.Screens)
        {
            if (string.Equals(screen.Name, name, StringComparison.OrdinalIgnoreCase))
                return screen;
        }

        foreach (var entity in project.Entities)
        {
            if (string.Equals(entity.Name, name, StringComparison.OrdinalIgnoreCase))
                return entity;
        }

        return null;
    }

    private static bool HasExtension(string? name, string extension) =>
        name is not null && name.EndsWith(extension, StringComparison.OrdinalIgnoreCase);

    private static string? DirectoryOf(string? path)
    {
        int lastSlash = path?.LastIndexOf('/') ?? -1;
        return lastSlash <= 0 ? null : path!.Substring(0, lastSlash);
    }

    private static string StripLeadingSegment(string path, string segment)
    {
        if (path.Length > segment.Length &&
            path[segment.Length] == '/' &&
            path.AsSpan(0, segment.Length).Equals(segment.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return path.Substring(segment.Length + 1);
        }

        return path;
    }
}
