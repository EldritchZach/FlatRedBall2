using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Builds real FRB2 objects from Glue <see cref="NamedObjectSave"/> data: constructs the instance,
/// applies its authored values, and optionally attaches and registers it.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="GlueScreen"/> and <see cref="GlueEntity"/> — both need it,
/// and keeping it standalone means it can be tested without a running engine.
/// <para>Anything it cannot build reports a diagnostic and is skipped. A project full of types later
/// phases own still loads and still shows what it can.</para>
/// </remarks>
public sealed class GlueObjectBuilder
{
    /// <summary>
    /// Glue member names that do not match the FRB2 property they set. Relative position is not
    /// listed here because it depends on attachment — see <see cref="ResolveMemberName"/>.
    /// </summary>
    private static readonly Dictionary<string, string> MemberAliases = new(StringComparer.Ordinal)
    {
        ["Visible"] = "IsVisible",
    };

    /// <summary>
    /// What the trimmer must keep on every constructible type: the properties instructions assign,
    /// and the parameterless constructor <see cref="Activator.CreateInstance(Type)"/> needs.
    /// </summary>
    /// <remarks>
    /// Rooting properties alone is not enough and fails silently. Without the constructor, a trimmed
    /// or AOT publish throws <see cref="MissingMethodException"/> inside
    /// <see cref="Activator.CreateInstance(Type)"/>, every <c>Create</c> returns null, and every
    /// screen loads completely empty — while an ordinary <c>dotnet build</c> stays clean, because
    /// the IL2067 that would flag it is only reported at publish time.
    /// </remarks>
    private const DynamicallyAccessedMemberTypes Rooted =
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor;

    private readonly ICollection<GlueLoadDiagnostic> _diagnostics;

    /// <summary>Creates a builder that reports what it cannot handle into <paramref name="diagnostics"/>.</summary>
    public GlueObjectBuilder(ICollection<GlueLoadDiagnostic> diagnostics) => _diagnostics = diagnostics;

    /// <summary>
    /// Constructs and configures an instance without attaching or registering it.
    /// </summary>
    /// <returns>The configured instance, or null if this build cannot construct that type.</returns>
    public object? Create(NamedObjectSave save, string? elementName = null)
    {
        var typeName = GlueTypeName.Parse(save.SourceClassType);

        if (!GlueTypeMap.TryCreate(typeName, out object? instance))
        {
            Warn($"'{save.InstanceName}' is a '{save.SourceClassType}', which cannot be built by " +
                 "this build. A later phase owns this type.", elementName);
            return null;
        }

        ApplyShapeVisibilityDefault(instance);
        ApplyInstructions(instance, save, elementName);

        // A Polygon starts with no points and its draw call bails below two, so it would be present,
        // positioned, and invisible with nothing to say why.
        if (instance is Collision.Polygon { Points.Count: < 2 })
        {
            Warn($"'{save.InstanceName}' is a Polygon with fewer than two points, so it will not " +
                 "render. Glue authors the geometry as a 'Points' instruction whose value is an " +
                 "array of \"x, y\" strings; decoding that shape is not supported yet.", elementName);
        }

        return instance;
    }

    /// <summary>
    /// Constructs an instance and adds it to <paramref name="container"/>, attaching it when the save
    /// says to. Attachment both parents the object and registers it for rendering.
    /// </summary>
    public object? AddTo(Entity container, NamedObjectSave save, string? elementName = null)
    {
        object? instance = Create(save, elementName);

        if (instance is not IAttachable attachable || !save.AttachToContainer)
            return instance;

        // Glue lets a shape be attached for position and rendering without taking part in the
        // entity's collision; FRB2's plain Add opts every shape in, so honour the flag. The
        // opt-out overload is generic over "attachable and collidable", which no single interface
        // expresses — hence the switch over the closed set of shape types.
        if (!save.IncludeInICollidable)
        {
            switch (instance)
            {
                case Collision.AARect rect: container.Add(rect, isDefaultCollision: false); return instance;
                case Collision.Circle circle: container.Add(circle, isDefaultCollision: false); return instance;
                case Collision.Polygon polygon: container.Add(polygon, isDefaultCollision: false); return instance;
            }
        }

        container.Add(attachable);
        return instance;
    }

    /// <summary>Constructs an instance and registers it directly on a screen.</summary>
    public object? AddTo(Screen container, NamedObjectSave save, string? elementName = null)
    {
        object? instance = Create(save, elementName);

        if (instance is Rendering.IRenderable renderable)
            container.Add(renderable);

        return instance;
    }

    /// <summary>
    /// FRB2 shapes default to invisible because they are primarily collision volumes; a shape
    /// authored in Glue is meant to be seen. Applied before instructions so an explicit
    /// <c>Visible</c> instruction still wins.
    /// </summary>
    private static void ApplyShapeVisibilityDefault(object instance)
    {
        switch (instance)
        {
            case Collision.AARect rect: rect.IsVisible = true; break;
            case Collision.Circle circle: circle.IsVisible = true; break;
            case Collision.Polygon polygon: polygon.IsVisible = true; break;
        }
    }

    /// <remarks>
    /// The reflected set is closed — it is exactly what <see cref="GlueTypeMap"/> can construct — so
    /// the properties are rooted explicitly rather than the warning being suppressed and hoped over.
    /// <para><b>Adding a type to <see cref="GlueTypeMap"/> means adding a matching
    /// <see cref="DynamicDependencyAttribute"/> here</b>, or its properties get trimmed away and
    /// every value assignment on it silently does nothing in a published AOT build.</para>
    /// </remarks>
    [DynamicDependency(Rooted, typeof(Collision.AARect))]
    [DynamicDependency(Rooted, typeof(Collision.Circle))]
    [DynamicDependency(Rooted, typeof(Collision.Polygon))]
    [DynamicDependency(Rooted, typeof(Rendering.Sprite))]
    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Every reflected type is rooted by the DynamicDependency attributes above, " +
                        "which cover exactly the closed set GlueTypeMap can construct.")]
    private void ApplyInstructions(object instance, NamedObjectSave save, string? elementName)
    {
        Type type = instance.GetType();

        foreach (var instruction in save.InstructionSaves)
        {
            if (string.IsNullOrEmpty(instruction.Member))
                continue;

            string memberName = ResolveMemberName(instruction.Member);
            var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);

            if (property is null || !property.CanWrite)
            {
                Warn($"'{save.InstanceName}' has no writable '{memberName}' " +
                     $"(from Glue member '{instruction.Member}'); the value was skipped.", elementName);
                continue;
            }

            if (!GlueValueConverter.TryConvert(instruction.Value, property.PropertyType, out object? converted))
            {
                Warn($"'{save.InstanceName}.{memberName}' could not take the authored value " +
                     $"'{instruction.Value}' as {property.PropertyType.Name}; the default was kept.",
                    elementName);
                continue;
            }

            property.SetValue(instance, converted);
        }
    }

    /// <summary>
    /// Maps a Glue member name onto the FRB2 property it sets.
    /// </summary>
    /// <remarks>
    /// Position is the interesting case, and it resolves more simply than it first appears. FRB1
    /// gives an attached object both an absolute <c>X</c> and a <c>RelativeX</c>, and its codegen
    /// picks between them at assignment time — it emits
    /// <c>if (obj.Parent == null) obj.X = v; else obj.RelativeX = v;</c> whenever the member has a
    /// relative counterpart. FRB2's <c>X</c> already <em>is</em> that branch: an offset from
    /// <c>Parent</c> when one is set, world space when not.
    /// <para>So both Glue members map to the same FRB2 property regardless of attachment, and no
    /// value is dropped. Dropping absolute values would misplace real authored content: DoorsDemo's
    /// player collision box and every Beefball score label are authored exactly this way.</para>
    /// </remarks>
    private static string ResolveMemberName(string glueMember)
    {
        if (glueMember is "RelativeX" or "RelativeY" or "RelativeZ")
            return glueMember["Relative".Length..];

        return MemberAliases.TryGetValue(glueMember, out string? alias) ? alias : glueMember;
    }

    private void Warn(string message, string? elementName) =>
        _diagnostics.Add(new GlueLoadDiagnostic(GlueDiagnosticSeverity.Warning, message, elementName));
}
