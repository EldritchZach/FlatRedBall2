using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall.AnimationChain.Content;

/// <summary>Time unit used by frames in a .achx file.</summary>
public enum TimeMeasurementUnit
{
    /// <summary>Undefined — treated identically to <see cref="Second"/> for compatibility.</summary>
    Undefined,
    /// <summary>Seconds.</summary>
    Second,
    /// <summary>Milliseconds.</summary>
    Millisecond
}

/// <summary>How texture coordinates are interpreted.</summary>
public enum TextureCoordinateType
{
    /// <summary>Coordinates are normalized (0 to 1).</summary>
    UV,
    /// <summary>Coordinates are raw pixel values.</summary>
    Pixel
}

/// <summary>
/// Deserialized representation of a .achx animation file. Use <see cref="FromFile(string)"/>
/// to load, then <see cref="ToAnimationChainList"/> to convert to runtime types.
/// For the most common case, prefer <see cref="AchxLoader.Load(string)"/> which handles
/// both steps and caches textures automatically.
/// </summary>
public class AnimationChainListSave
{
    /// <summary>
    /// Whether texture file paths stored in frames are relative to the .achx file location.
    /// Defaults to <c>true</c> (the standard .achx convention).
    /// </summary>
    public bool FileRelativeTextures = true;

    /// <summary>The time unit used by all frames in this file.</summary>
    public TimeMeasurementUnit TimeMeasurementUnit = TimeMeasurementUnit.Second;

    /// <summary>How texture coordinates in frames are specified.</summary>
    public TextureCoordinateType CoordinateType = TextureCoordinateType.UV;

    /// <summary>All animation chains in this file.</summary>
    public List<AnimationChainSave> AnimationChains = new();

    /// <summary>
    /// Absolute path of the .achx file. Set automatically by <see cref="FromFile(string)"/>.
    /// Used to resolve relative texture paths in <see cref="ToAnimationChainList"/>.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Path of the project file the .achx belongs to, relative to the .achx location.
    /// Round-tripped by tooling but ignored at runtime.
    /// </summary>
    public string? ProjectFile { get; set; }

    /// <summary>
    /// Loads a .achx file from disk. Use this when you have an absolute path and want to
    /// bypass any custom stream provider.
    /// </summary>
    public static AnimationChainListSave FromFile(string path)
        => FromFile(path, File.OpenRead!);

    /// <summary>
    /// Loads a .achx file via a custom stream provider. Useful for non-filesystem environments
    /// (Blazor WASM, unit tests with in-memory XML) where <see cref="File.OpenRead"/> is
    /// unavailable or undesirable.
    /// </summary>
    /// <param name="filePath">Path passed to <paramref name="streamProvider"/>.</param>
    /// <param name="streamProvider">Returns a readable stream for the given path.</param>
    public static AnimationChainListSave FromFile(string filePath, Func<string, Stream> streamProvider)
    {
        using var stream = streamProvider(filePath);
        var result = ParseXml(XDocument.Load(stream));
        // Store the absolute path so ToAnimationChainList always produces absolute texture paths,
        // preventing double-resolution when callers (e.g. AchxLoader) also combine with achxDir.
        result.FileName = Path.GetFullPath(filePath);
        return result;
    }

    /// <summary>
    /// Parses .achx XML from an already-open <paramref name="stream"/>. <see cref="FileName"/>
    /// is set to <see cref="string.Empty"/>; if <see cref="FileRelativeTextures"/> is <c>true</c>,
    /// texture paths in the file are passed through as-is with no directory prefix prepended.
    /// The caller retains ownership of <paramref name="stream"/> and is responsible for disposing it.
    /// </summary>
    public static AnimationChainListSave FromStream(Stream stream)
        => ParseXml(XDocument.Load(stream));

    /// <summary>
    /// Parses .achx XML from an in-memory string. <see cref="FileName"/> is set to
    /// <see cref="string.Empty"/>; if <see cref="FileRelativeTextures"/> is <c>true</c>,
    /// texture paths in the file are passed through as-is with no directory prefix prepended.
    /// </summary>
    public static AnimationChainListSave FromString(string xml)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        return FromStream(stream);
    }

    /// <summary>
    /// Loads a .achj (JSON) file from disk. JSON counterpart of <see cref="FromFile(string)"/>.
    /// </summary>
    public static AnimationChainListSave FromJsonFile(string path)
        => FromJsonFile(path, File.OpenRead!);

    /// <summary>
    /// Loads a .achj file via a custom stream provider. JSON counterpart of
    /// <see cref="FromFile(string, Func{string, Stream})"/>.
    /// </summary>
    public static AnimationChainListSave FromJsonFile(string filePath, Func<string, Stream> streamProvider)
    {
        using var stream = streamProvider(filePath);
        var result = ParseJson(JsonNode.Parse(stream)!.AsObject());
        result.FileName = Path.GetFullPath(filePath);
        return result;
    }

    /// <summary>Parses .achj JSON from an already-open <paramref name="stream"/>, same contract as <see cref="FromStream(Stream)"/>.</summary>
    public static AnimationChainListSave FromJsonStream(Stream stream)
        => ParseJson(JsonNode.Parse(stream)!.AsObject());

    /// <summary>Parses .achj JSON from an in-memory string, same contract as <see cref="FromString(string)"/>.</summary>
    public static AnimationChainListSave FromJsonString(string json)
        => ParseJson(JsonNode.Parse(json)!.AsObject());

    /// <summary>
    /// Parses .achx (XML) or .achj (JSON) from <paramref name="stream"/>, detecting the dialect
    /// from its content rather than a file extension -- for callers (e.g. <see cref="AchxLoader"/>'s
    /// stream overload) that only have a stream and no path to inspect. Reads the stream fully
    /// into memory first, so this works even on a non-seekable stream (network fetch, WASM).
    /// The caller retains ownership of <paramref name="stream"/> and is responsible for disposing it.
    /// </summary>
    public static AnimationChainListSave FromDetectedStream(Stream stream)
    {
        string text;
        using (var reader = new StreamReader(stream, leaveOpen: true))
            text = reader.ReadToEnd();

        return LooksLikeJson(text) ? FromJsonString(text) : FromString(text);
    }

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.AsSpan().TrimStart();
        return trimmed.Length > 0 && trimmed[0] == '{';
    }

    private static AnimationChainListSave ParseXml(XDocument doc)
    {
        var root = doc.Root!;
        var result = new AnimationChainListSave();

        var frt = root.Element("FileRelativeTextures");
        if (frt != null) result.FileRelativeTextures = bool.Parse(frt.Value);

        var tmu = root.Element("TimeMeasurementUnit");
        if (tmu != null) result.TimeMeasurementUnit = Enum.Parse<TimeMeasurementUnit>(tmu.Value);

        var ct = root.Element("CoordinateType");
        if (ct != null) result.CoordinateType = Enum.Parse<TextureCoordinateType>(ct.Value);

        foreach (var chainEl in root.Elements("AnimationChain"))
        {
            var chain = new AnimationChainSave
            {
                Name = (string?)chainEl.Element("Name") ?? string.Empty
            };
            foreach (var frameEl in chainEl.Elements("Frame"))
                chain.Frames.Add(ParseFrame(frameEl));
            result.AnimationChains.Add(chain);
        }

        var projectFileEl = root.Element("ProjectFile");
        if (projectFileEl != null) result.ProjectFile = projectFileEl.Value;

        return result;
    }

    /// <summary>
    /// Writes this save to a .achx file using the FRB1-compatible XML dialect so existing
    /// tooling and engine versions can round-trip the file.
    /// </summary>
    public void Save(string path)
    {
        var root = new XElement("AnimationChainArraySave",
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
            new XElement("FileRelativeTextures", FileRelativeTextures ? "true" : "false"),
            new XElement("TimeMeasurementUnit", TimeMeasurementUnit.ToString()),
            new XElement("CoordinateType", CoordinateType.ToString()));

        foreach (var chain in AnimationChains)
        {
            var chainEl = new XElement("AnimationChain", new XElement("Name", chain.Name));
            foreach (var frame in chain.Frames)
                chainEl.Add(WriteFrame(frame));
            root.Add(chainEl);
        }

        if (ProjectFile != null)
            root.Add(new XElement("ProjectFile", ProjectFile));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        using var stream = File.Create(path);
        doc.Save(stream);
    }

    /// <summary>
    /// Writes this save as a .achj (JSON) file. JSON counterpart of <see cref="Save(string)"/>.
    /// </summary>
    public void SaveJson(string path)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        ToJsonNode().WriteTo(writer);
    }

    /// <summary>Serializes this save to a .achj JSON string.</summary>
    public string ToJsonString() => ToJsonNode().ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    private JsonObject ToJsonNode()
    {
        var root = new JsonObject
        {
            ["fileRelativeTextures"] = FileRelativeTextures,
            ["timeMeasurementUnit"] = TimeMeasurementUnit.ToString(),
            ["coordinateType"] = CoordinateType.ToString(),
        };

        var chainsArray = new JsonArray();
        foreach (var chain in AnimationChains)
        {
            var framesArray = new JsonArray();
            foreach (var frame in chain.Frames)
                framesArray.Add((JsonNode)WriteFrameJson(frame));

            chainsArray.Add((JsonNode)new JsonObject
            {
                ["name"] = chain.Name,
                ["frames"] = framesArray,
            });
        }
        root["animationChains"] = chainsArray;

        if (ProjectFile != null)
            root["projectFile"] = ProjectFile;

        return root;
    }

    private static JsonObject WriteFrameJson(AnimationFrameSave frame)
    {
        var obj = new JsonObject
        {
            ["textureName"] = frame.TextureName,
            ["frameLength"] = frame.FrameLength,
            ["leftCoordinate"] = frame.LeftCoordinate,
            ["rightCoordinate"] = frame.RightCoordinate,
            ["topCoordinate"] = frame.TopCoordinate,
            ["bottomCoordinate"] = frame.BottomCoordinate,
        };
        if (frame.FlipHorizontal) obj["flipHorizontal"] = true;
        if (frame.FlipVertical) obj["flipVertical"] = true;
        if (frame.FlipDiagonal) obj["flipDiagonal"] = true;
        if (frame.RelativeX != 0f) obj["relativeX"] = frame.RelativeX;
        if (frame.RelativeY != 0f) obj["relativeY"] = frame.RelativeY;
        if (frame.Red.HasValue) obj["red"] = frame.Red.Value;
        if (frame.Green.HasValue) obj["green"] = frame.Green.Value;
        if (frame.Blue.HasValue) obj["blue"] = frame.Blue.Value;
        if (frame.Alpha.HasValue) obj["alpha"] = frame.Alpha.Value;
        if (frame.ColorOperation.HasValue) obj["colorOperation"] = frame.ColorOperation.Value.ToString();
        if (frame.ShapesSave is { } shapes) obj["shapes"] = WriteShapesJson(shapes);
        return obj;
    }

    private static JsonObject WriteShapesJson(ShapesSave shapes)
    {
        var rectsArray = new JsonArray();
        foreach (var r in shapes.AARectSaves)
            rectsArray.Add((JsonNode)new JsonObject
            {
                ["name"] = r.Name, ["x"] = r.X, ["y"] = r.Y, ["scaleX"] = r.ScaleX, ["scaleY"] = r.ScaleY,
            });

        var circlesArray = new JsonArray();
        foreach (var c in shapes.CircleSaves)
            circlesArray.Add((JsonNode)new JsonObject
            {
                ["name"] = c.Name, ["x"] = c.X, ["y"] = c.Y, ["radius"] = c.Radius,
            });

        var polysArray = new JsonArray();
        foreach (var p in shapes.PolygonSaves)
        {
            var pointsArray = new JsonArray();
            foreach (var v in p.Points)
                pointsArray.Add((JsonNode)new JsonObject { ["x"] = v.X, ["y"] = v.Y });

            polysArray.Add((JsonNode)new JsonObject
            {
                ["name"] = p.Name, ["x"] = p.X, ["y"] = p.Y,
                ["points"] = pointsArray,
            });
        }

        return new JsonObject
        {
            ["rectangles"] = rectsArray,
            ["circles"] = circlesArray,
            ["polygons"] = polysArray,
        };
    }

    private static AnimationChainListSave ParseJson(JsonObject root)
    {
        var result = new AnimationChainListSave();

        if (root["fileRelativeTextures"] is JsonValue frt)
            result.FileRelativeTextures = frt.GetValue<bool>();

        if (root["timeMeasurementUnit"] is JsonValue tmu)
            result.TimeMeasurementUnit = Enum.Parse<TimeMeasurementUnit>(tmu.GetValue<string>()!);

        if (root["coordinateType"] is JsonValue ct)
            result.CoordinateType = Enum.Parse<TextureCoordinateType>(ct.GetValue<string>()!);

        if (root["animationChains"] is JsonArray chainsArray)
        {
            foreach (var chainNode in chainsArray)
            {
                var chainObj = chainNode!.AsObject();
                var chain = new AnimationChainSave
                {
                    Name = chainObj["name"]?.GetValue<string>() ?? string.Empty
                };

                if (chainObj["frames"] is JsonArray framesArray)
                    foreach (var frameNode in framesArray)
                        chain.Frames.Add(ParseFrameJson(frameNode!.AsObject()));

                result.AnimationChains.Add(chain);
            }
        }

        if (root["projectFile"] is JsonValue pf)
            result.ProjectFile = pf.GetValue<string>();

        return result;
    }

    private static AnimationFrameSave ParseFrameJson(JsonObject el)
    {
        var frame = new AnimationFrameSave
        {
            TextureName = el["textureName"]?.GetValue<string>() ?? string.Empty,
            FrameLength = FloatProp(el, "frameLength"),
            LeftCoordinate = FloatProp(el, "leftCoordinate"),
            RightCoordinate = FloatProp(el, "rightCoordinate", 1f),
            TopCoordinate = FloatProp(el, "topCoordinate"),
            BottomCoordinate = FloatProp(el, "bottomCoordinate", 1f),
            FlipHorizontal = BoolProp(el, "flipHorizontal"),
            FlipVertical = BoolProp(el, "flipVertical"),
            FlipDiagonal = BoolProp(el, "flipDiagonal"),
            RelativeX = FloatProp(el, "relativeX"),
            RelativeY = FloatProp(el, "relativeY"),
            Red = IntPropNullable(el, "red"),
            Green = IntPropNullable(el, "green"),
            Blue = IntPropNullable(el, "blue"),
            Alpha = IntPropNullable(el, "alpha"),
            ColorOperation = ColorOperationProp(el, "colorOperation"),
        };

        if (el["shapes"] is JsonObject shapesObj)
            frame.ShapesSave = ParseShapesJson(shapesObj);

        return frame;
    }

    private static ShapesSave ParseShapesJson(JsonObject el)
    {
        var shapes = new ShapesSave();

        if (el["rectangles"] is JsonArray rectsArray)
        {
            foreach (var node in rectsArray)
            {
                var r = node!.AsObject();
                shapes.Shapes.Add(new AARectSave
                {
                    Name = r["name"]?.GetValue<string>() ?? string.Empty,
                    X = FloatProp(r, "x"),
                    Y = FloatProp(r, "y"),
                    ScaleX = FloatProp(r, "scaleX", 16f),
                    ScaleY = FloatProp(r, "scaleY", 16f),
                });
            }
        }

        if (el["circles"] is JsonArray circlesArray)
        {
            foreach (var node in circlesArray)
            {
                var c = node!.AsObject();
                shapes.Shapes.Add(new CircleSave
                {
                    Name = c["name"]?.GetValue<string>() ?? string.Empty,
                    X = FloatProp(c, "x"),
                    Y = FloatProp(c, "y"),
                    Radius = FloatProp(c, "radius", 16f),
                });
            }
        }

        if (el["polygons"] is JsonArray polysArray)
        {
            foreach (var node in polysArray)
            {
                var p = node!.AsObject();
                var poly = new PolygonSave
                {
                    Name = p["name"]?.GetValue<string>() ?? string.Empty,
                    X = FloatProp(p, "x"),
                    Y = FloatProp(p, "y"),
                };
                if (p["points"] is JsonArray pointsArray)
                    foreach (var ptNode in pointsArray)
                    {
                        var pt = ptNode!.AsObject();
                        poly.Points.Add(new Vector2Save { X = FloatProp(pt, "x"), Y = FloatProp(pt, "y") });
                    }
                shapes.Shapes.Add(poly);
            }
        }

        return shapes;
    }

    private static float FloatProp(JsonObject parent, string name, float defaultValue = 0f) =>
        parent[name] is JsonValue v ? v.GetValue<float>() : defaultValue;

    private static bool BoolProp(JsonObject parent, string name, bool defaultValue = false) =>
        parent[name] is JsonValue v ? v.GetValue<bool>() : defaultValue;

    private static int? IntPropNullable(JsonObject parent, string name) =>
        parent[name] is JsonValue v ? v.GetValue<int>() : null;

    private static ColorOperation? ColorOperationProp(JsonObject parent, string name) =>
        parent[name] is JsonValue v ? Enum.Parse<ColorOperation>(v.GetValue<string>()!) : null;

    /// <summary>
    /// Converts this save to a runtime <see cref="AnimationChainList"/>. Texture paths are
    /// resolved relative to the .achx file location (when <see cref="FileRelativeTextures"/>
    /// is <c>true</c>) and passed to <paramref name="textureLoader"/>.
    /// <para>
    /// For most callers, prefer <see cref="AchxLoader.Load(string)"/> which wraps this call
    /// and adds caching so the same spritesheet is not uploaded more than once.
    /// </para>
    /// </summary>
    /// <param name="textureLoader">
    /// Called with the resolved absolute-or-relative texture path. May return <c>null</c>
    /// if the texture is unavailable — the frame will have a <c>null</c> texture.
    /// </param>
    public AnimationChainList ToAnimationChainList(Func<string, Texture2D?> textureLoader)
    {
        string achxDir = string.IsNullOrEmpty(FileName) ? "" : Path.GetDirectoryName(FileName) ?? "";

        return BuildList(frameSave =>
        {
            if (string.IsNullOrEmpty(frameSave.TextureName)) return null;
            string texPath = FileRelativeTextures && !string.IsNullOrEmpty(achxDir)
                ? Path.Combine(achxDir, frameSave.TextureName)
                : frameSave.TextureName;
            return textureLoader(texPath);
        });
    }

    private AnimationChainList BuildList(Func<AnimationFrameSave, Texture2D?> loadTexture)
    {
        float frameLengthDivisor = TimeMeasurementUnit == TimeMeasurementUnit.Millisecond ? 1000f : 1f;
        var list = new AnimationChainList { Name = FileName };

        foreach (var chainSave in AnimationChains)
        {
            var chain = new AnimationChain { Name = chainSave.Name };

            // Sticky color resolution: a frame that omits a channel inherits the most recent
            // explicitly-set value from an earlier frame in this chain, rather than resetting to
            // null. Mirrors the Animation Editor's EffectiveFrameColor.ResolveAll.
            int? stickyRed = null, stickyGreen = null, stickyBlue = null, stickyAlpha = null;
            ColorOperation? stickyOperation = null;

            foreach (var frameSave in chainSave.Frames)
            {
                stickyRed       = frameSave.Red           ?? stickyRed;
                stickyGreen     = frameSave.Green         ?? stickyGreen;
                stickyBlue      = frameSave.Blue          ?? stickyBlue;
                stickyAlpha     = frameSave.Alpha         ?? stickyAlpha;
                stickyOperation = frameSave.ColorOperation ?? stickyOperation;

                var frame = new AnimationFrame
                {
                    TextureName = frameSave.TextureName,
                    FrameLength = TimeSpan.FromSeconds(frameSave.FrameLength / frameLengthDivisor),
                    FlipHorizontal = frameSave.FlipHorizontal,
                    FlipVertical = frameSave.FlipVertical,
                    FlipDiagonal = frameSave.FlipDiagonal,
                    RelativeX = frameSave.RelativeX,
                    RelativeY = frameSave.RelativeY,
                    Red = stickyRed,
                    Green = stickyGreen,
                    Blue = stickyBlue,
                    Alpha = stickyAlpha,
                    ColorOperation = stickyOperation,
                };

                frame.Texture = loadTexture(frameSave);

                if (frame.Texture != null)
                {
                    int left, top, width, height;
                    if (CoordinateType == TextureCoordinateType.Pixel)
                    {
                        left   = (int)frameSave.LeftCoordinate;
                        top    = (int)frameSave.TopCoordinate;
                        width  = (int)(frameSave.RightCoordinate  - frameSave.LeftCoordinate);
                        height = (int)(frameSave.BottomCoordinate - frameSave.TopCoordinate);
                    }
                    else // UV
                    {
                        left   = (int)(frameSave.LeftCoordinate   * frame.Texture.Width);
                        top    = (int)(frameSave.TopCoordinate    * frame.Texture.Height);
                        width  = (int)((frameSave.RightCoordinate  - frameSave.LeftCoordinate) * frame.Texture.Width);
                        height = (int)((frameSave.BottomCoordinate - frameSave.TopCoordinate)  * frame.Texture.Height);
                    }

                    if (width > 0 && height > 0)
                        frame.SourceRectangle = new Rectangle(left, top, width, height);
                }

                AppendShapes(frame, frameSave.ShapesSave);
                chain.Add(frame);
            }

            list.Add(chain);
        }

        return list;
    }

    private static void AppendShapes(AnimationFrame frame, ShapesSave? shapes)
    {
        if (shapes == null) return;

        foreach (var shape in shapes.Shapes)
        {
            switch (shape)
            {
                case AARectSave rect:
                    ValidateName(rect.Name, "AARectSave");
                    frame.Shapes.Add(new AnimationAARectFrame
                    {
                        Name = rect.Name,
                        RelativeX = rect.X,
                        RelativeY = rect.Y,
                        Width = rect.ScaleX * 2f,
                        Height = rect.ScaleY * 2f,
                    });
                    break;
                case CircleSave circle:
                    ValidateName(circle.Name, "CircleSave");
                    frame.Shapes.Add(new AnimationCircleFrame
                    {
                        Name = circle.Name,
                        RelativeX = circle.X,
                        RelativeY = circle.Y,
                        Radius = circle.Radius,
                    });
                    break;
                case PolygonSave poly:
                    ValidateName(poly.Name, "PolygonSave");
                    var points = new System.Numerics.Vector2[poly.Points.Count];
                    for (int i = 0; i < poly.Points.Count; i++)
                        points[i] = new System.Numerics.Vector2(poly.Points[i].X, poly.Points[i].Y);
                    frame.Shapes.Add(new AnimationPolygonFrame
                    {
                        Name = poly.Name,
                        RelativeX = poly.X,
                        RelativeY = poly.Y,
                        Points = points,
                    });
                    break;
            }
        }
    }

    private static void ValidateName(string name, string elementType)
    {
        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException(
                $"{elementType} in .achx ShapesSave is missing a Name. Per-frame shapes must have non-empty unique names.");
    }

    private static AnimationFrameSave ParseFrame(XElement el)
    {
        var frame = new AnimationFrameSave
        {
            FlipHorizontal = BoolEl(el, "FlipHorizontal"),
            FlipVertical   = BoolEl(el, "FlipVertical"),
            FlipDiagonal   = BoolEl(el, "FlipDiagonal"),
            TextureName    = (string?)el.Element("TextureName") ?? string.Empty,
            FrameLength    = FloatEl(el, "FrameLength"),
            LeftCoordinate = FloatEl(el, "LeftCoordinate"),
            RightCoordinate  = FloatEl(el, "RightCoordinate",  1f),
            TopCoordinate    = FloatEl(el, "TopCoordinate"),
            BottomCoordinate = FloatEl(el, "BottomCoordinate", 1f),
            RelativeX = FloatEl(el, "RelativeX"),
            RelativeY = FloatEl(el, "RelativeY"),
            Red   = IntElNullable(el, "Red"),
            Green = IntElNullable(el, "Green"),
            Blue  = IntElNullable(el, "Blue"),
            Alpha = IntElNullable(el, "Alpha"),
            ColorOperation = ColorOperationEl(el, "ColorOperation"),
        };

        // Frame <Name>/<HasCustomName> in legacy .achx are intentionally ignored: a frame's
        // identity is its index, so the editor always shows a positional "Frame N" label.

        // New dialect: <Shapes> wrapper; old dialect: <ShapeCollectionSave> wrapper.
        var shapesEl = el.Element("Shapes") ?? el.Element("ShapeCollectionSave");
        if (shapesEl != null)
            frame.ShapesSave = ParseShapes(shapesEl);

        return frame;
    }

    private static ShapesSave ParseShapes(XElement el)
    {
        var shapes = new ShapesSave();

        var newShapesEl = el.Element("Shapes");
        if (newShapesEl != null)
        {
            foreach (var child in newShapesEl.Elements())
            {
                switch (child.Name.LocalName)
                {
                    case "AxisAlignedRectangleSave":
                        shapes.Shapes.Add(new AARectSave
                        {
                            Name = (string?)child.Element("Name") ?? string.Empty,
                            X = FloatEl(child, "X"),
                            Y = FloatEl(child, "Y"),
                            ScaleX = FloatEl(child, "ScaleX", 16f),
                            ScaleY = FloatEl(child, "ScaleY", 16f),
                        });
                        break;
                    case "CircleSave":
                        shapes.Shapes.Add(new CircleSave
                        {
                            Name = (string?)child.Element("Name") ?? string.Empty,
                            X = FloatEl(child, "X"),
                            Y = FloatEl(child, "Y"),
                            Radius = FloatEl(child, "Radius", 16f),
                        });
                        break;
                    case "PolygonSave":
                        var poly = new PolygonSave
                        {
                            Name = (string?)child.Element("Name") ?? string.Empty,
                            X = FloatEl(child, "X"),
                            Y = FloatEl(child, "Y"),
                        };
                        var polyPointsEl = child.Element("Points");
                        if (polyPointsEl != null)
                            foreach (var v in polyPointsEl.Elements("Vector2Save"))
                                poly.Points.Add(new Vector2Save { X = FloatEl(v, "X"), Y = FloatEl(v, "Y") });
                        shapes.Shapes.Add(poly);
                        break;
                }
            }
            return shapes;
        }

        // Old format fallback: separate typed containers (rects, then circles, then polygons).
        var aarctsEl = el.Element("AxisAlignedRectangleSaves");
        if (aarctsEl != null)
            foreach (var r in aarctsEl.Elements("AxisAlignedRectangleSave"))
                shapes.Shapes.Add(new AARectSave
                {
                    Name = (string?)r.Element("Name") ?? string.Empty,
                    X = FloatEl(r, "X"), Y = FloatEl(r, "Y"),
                    ScaleX = FloatEl(r, "ScaleX", 16f), ScaleY = FloatEl(r, "ScaleY", 16f),
                });

        var circlesEl = el.Element("CircleSaves");
        if (circlesEl != null)
            foreach (var c in circlesEl.Elements("CircleSave"))
                shapes.Shapes.Add(new CircleSave
                {
                    Name = (string?)c.Element("Name") ?? string.Empty,
                    X = FloatEl(c, "X"), Y = FloatEl(c, "Y"),
                    Radius = FloatEl(c, "Radius", 16f),
                });

        var polysEl = el.Element("PolygonSaves");
        if (polysEl != null)
        {
            foreach (var p in polysEl.Elements("PolygonSave"))
            {
                var poly = new PolygonSave
                {
                    Name = (string?)p.Element("Name") ?? string.Empty,
                    X = FloatEl(p, "X"), Y = FloatEl(p, "Y"),
                };
                var pts = p.Element("Points");
                if (pts != null)
                    foreach (var v in pts.Elements("Vector2Save"))
                        poly.Points.Add(new Vector2Save { X = FloatEl(v, "X"), Y = FloatEl(v, "Y") });
                shapes.Shapes.Add(poly);
            }
        }

        return shapes;
    }

    private static XElement WriteFrame(AnimationFrameSave frame)
    {
        var el = new XElement("Frame");
        if (frame.FlipHorizontal) el.Add(new XElement("FlipHorizontal", "true"));
        if (frame.FlipVertical)   el.Add(new XElement("FlipVertical", "true"));
        if (frame.FlipDiagonal)   el.Add(new XElement("FlipDiagonal", "true"));
        el.Add(new XElement("TextureName", frame.TextureName));
        el.Add(new XElement("FrameLength", FloatStr(frame.FrameLength)));
        el.Add(new XElement("LeftCoordinate",   FloatStr(frame.LeftCoordinate)));
        el.Add(new XElement("RightCoordinate",  FloatStr(frame.RightCoordinate)));
        el.Add(new XElement("TopCoordinate",    FloatStr(frame.TopCoordinate)));
        el.Add(new XElement("BottomCoordinate", FloatStr(frame.BottomCoordinate)));
        if (frame.RelativeX != 0f) el.Add(new XElement("RelativeX", FloatStr(frame.RelativeX)));
        if (frame.RelativeY != 0f) el.Add(new XElement("RelativeY", FloatStr(frame.RelativeY)));
        if (frame.Red.HasValue)   el.Add(new XElement("Red", frame.Red.Value));
        if (frame.Green.HasValue) el.Add(new XElement("Green", frame.Green.Value));
        if (frame.Blue.HasValue)  el.Add(new XElement("Blue", frame.Blue.Value));
        if (frame.Alpha.HasValue) el.Add(new XElement("Alpha", frame.Alpha.Value));
        if (frame.ColorOperation.HasValue) el.Add(new XElement("ColorOperation", frame.ColorOperation.Value.ToString()));
        el.Add(WriteShapesElement(frame.ShapesSave));
        return el;
    }

    private static XElement WriteShapesElement(ShapesSave? shapes)
    {
        shapes ??= new ShapesSave();
        var shapesEl = new XElement("ShapeCollectionSave");
        var innerEl = new XElement("Shapes");
        foreach (var shape in shapes.Shapes)
        {
            switch (shape)
            {
                case AARectSave r:
                    innerEl.Add(new XElement("AxisAlignedRectangleSave",
                        new XElement("Name", r.Name), new XElement("X", FloatStr(r.X)), new XElement("Y", FloatStr(r.Y)),
                        new XElement("ScaleX", FloatStr(r.ScaleX)), new XElement("ScaleY", FloatStr(r.ScaleY))));
                    break;
                case CircleSave c:
                    innerEl.Add(new XElement("CircleSave",
                        new XElement("Name", c.Name), new XElement("X", FloatStr(c.X)), new XElement("Y", FloatStr(c.Y)),
                        new XElement("Radius", FloatStr(c.Radius))));
                    break;
                case PolygonSave p:
                    var polyEl = new XElement("PolygonSave",
                        new XElement("Name", p.Name), new XElement("X", FloatStr(p.X)), new XElement("Y", FloatStr(p.Y)));
                    var ptsEl = new XElement("Points");
                    foreach (var v in p.Points)
                        ptsEl.Add(new XElement("Vector2Save", new XElement("X", FloatStr(v.X)), new XElement("Y", FloatStr(v.Y))));
                    polyEl.Add(ptsEl);
                    innerEl.Add(polyEl);
                    break;
            }
        }
        shapesEl.Add(innerEl);
        return shapesEl;
    }

    private static float FloatEl(XElement parent, string name, float defaultValue = 0f)
    {
        var el = parent.Element(name);
        return el != null ? float.Parse(el.Value, CultureInfo.InvariantCulture) : defaultValue;
    }

    private static bool BoolEl(XElement parent, string name, bool defaultValue = false)
    {
        var el = parent.Element(name);
        return el != null ? bool.Parse(el.Value) : defaultValue;
    }

    private static int? IntElNullable(XElement parent, string name)
    {
        var el = parent.Element(name);
        return el != null ? int.Parse(el.Value, CultureInfo.InvariantCulture) : null;
    }

    private static ColorOperation? ColorOperationEl(XElement parent, string name)
    {
        var el = parent.Element(name);
        return el != null ? Enum.Parse<ColorOperation>(el.Value) : null;
    }

    private static string FloatStr(float v) => v.ToString("G9", CultureInfo.InvariantCulture);
}
