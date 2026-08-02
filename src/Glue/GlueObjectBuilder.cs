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

        if (!GlueTypeMap.TryGetType(typeName, out var type))
        {
            Warn($"'{save.InstanceName}' is a '{save.SourceClassType}', which cannot be built by " +
                 "this build. A later phase owns this type.", elementName);
            return null;
        }

        object instance;
        try
        {
            instance = Activator.CreateInstance(type)!;
        }
        catch (Exception exception) when (exception is MissingMethodException or MemberAccessException)
        {
            Warn($"'{save.InstanceName}' is a '{type.Name}', which has no parameterless constructor.",
                elementName);
            return null;
        }

        ApplyShapeVisibilityDefault(instance);
        ApplyInstructions(instance, save, elementName);

        return instance;
    }

    /// <summary>
    /// Constructs an instance and adds it to <paramref name="container"/>, attaching it when the save
    /// says to. Attachment both parents the object and registers it for rendering.
    /// </summary>
    public object? AddTo(Entity container, NamedObjectSave save, string? elementName = null)
    {
        object? instance = Create(save, elementName);

        if (instance is IAttachable attachable && save.AttachToContainer)
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
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Collision.AARect))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Collision.Circle))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Collision.Polygon))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Rendering.Sprite))]
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

            string? memberName = ResolveMemberName(instruction.Member, save, elementName);
            if (memberName is null)
                continue;

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
    /// Maps a Glue member name onto the FRB2 property it sets, or null when it should be skipped.
    /// </summary>
    /// <remarks>
    /// Position is the interesting case. FRB1 gives an attached object both an absolute <c>X</c> and a
    /// <c>RelativeX</c>; FRB2 has one <c>X</c> that already <em>means</em> the offset whenever a
    /// parent is set. So the two Glue members collapse onto one property, chosen by attachment — and
    /// an absolute value on an attached object is dropped, because honouring it would silently place
    /// the object at that value as an offset instead.
    /// </remarks>
    private string? ResolveMemberName(string glueMember, NamedObjectSave save, string? elementName)
    {
        if (glueMember is "RelativeX" or "RelativeY" or "RelativeZ")
            return glueMember["Relative".Length..];

        if (save.AttachToContainer && glueMember is "X" or "Y" or "Z")
        {
            Warn($"'{save.InstanceName}' is attached to its container but sets an absolute " +
                 $"'{glueMember}'. Attachment wins, so the value was ignored — author it as " +
                 $"'Relative{glueMember}' if an offset was intended.", elementName);
            return null;
        }

        return MemberAliases.TryGetValue(glueMember, out string? alias) ? alias : glueMember;
    }

    private void Warn(string message, string? elementName) =>
        _diagnostics.Add(new GlueLoadDiagnostic(GlueDiagnosticSeverity.Warning, message, elementName));
}
