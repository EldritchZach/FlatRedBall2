using System.Collections.Generic;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// An FRB2 <see cref="Screen"/> built from a Glue <see cref="ScreenSave"/> rather than from a
/// hand-written subclass. Every loaded screen is this one type — what distinguishes them is the data
/// in <see cref="Save"/>, not their class.
/// </summary>
/// <remarks>
/// Start a loaded project by handing the resolved start-up screen to the normal screen machinery:
/// <code>
/// var result = GlueProjectLoader.Load(glujPath);
/// service.Start&lt;GlueScreen&gt;(screen =&gt; screen.Save = result.StartUpScreen);
/// </code>
/// </remarks>
public class GlueScreen : Screen
{
    private readonly Dictionary<string, object> _objects = new();
    private readonly List<GlueLoadDiagnostic> _buildDiagnostics = new();

    /// <summary>
    /// The screen data this was built from. Assign it before <c>CustomInitialize</c> runs — the
    /// <c>configure</c> callback on <c>Start</c> and <c>MoveToScreen</c> is the intended place.
    /// </summary>
    public ScreenSave? Save { get; set; }

    /// <summary>The Glue element name, in backslash form (<c>Screens\Level1</c>).</summary>
    public string? GlueName => Save?.Name;

    /// <summary>
    /// The objects built from <see cref="Save"/>, keyed by their Glue instance name. Objects whose
    /// type a later phase owns are absent; see <see cref="BuildDiagnostics"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object> Objects => _objects;

    /// <summary>What could not be built, and why. Warnings here are expected, not failures.</summary>
    public IReadOnlyList<GlueLoadDiagnostic> BuildDiagnostics => _buildDiagnostics;

    /// <inheritdoc />
    public override void CustomInitialize() => BuildObjects();

    /// <summary>
    /// Builds every object in <see cref="Save"/> and registers it on this screen. Called by
    /// <see cref="CustomInitialize"/>; safe to call directly in tests, where no engine is running.
    /// </summary>
    public void BuildObjects()
    {
        // Unregister anything a previous build added, so a rebuild (hot reload restarts one) does
        // not leave duplicates of every object behind it.
        foreach (var previous in GlueElementBuilder.Flatten(_objects.Values))
        {
            if (previous is Rendering.IRenderable renderable)
                Remove(renderable);
        }

        _objects.Clear();
        _buildDiagnostics.Clear();

        if (Save is null)
            return;

        GlueElementBuilder.Build(Save.NamedObjects, Save.Name, _objects, _buildDiagnostics,
            addSingle: (builder, save) => builder.AddTo(this, save, Save.Name));
    }

    /// <inheritdoc />
    public override string ToString() => GlueName ?? nameof(GlueScreen);
}

/// <summary>
/// An FRB2 <see cref="Entity"/> built from a Glue <see cref="EntitySave"/>. As with
/// <see cref="GlueScreen"/>, every loaded entity shares this one type and is distinguished by data.
/// </summary>
public class GlueEntity : Entity
{
    private readonly Dictionary<string, object> _objects = new();
    private readonly List<GlueLoadDiagnostic> _buildDiagnostics = new();

    /// <summary>The entity data this was built from. Assign it before <c>CustomInitialize</c> runs.</summary>
    public EntitySave? Save { get; set; }

    /// <summary>The Glue element name, in backslash form (<c>Entities\Player</c>).</summary>
    public string? GlueName => Save?.Name;

    /// <summary>The objects built from <see cref="Save"/>, keyed by their Glue instance name.</summary>
    public IReadOnlyDictionary<string, object> Objects => _objects;

    /// <summary>What could not be built, and why. Warnings here are expected, not failures.</summary>
    public IReadOnlyList<GlueLoadDiagnostic> BuildDiagnostics => _buildDiagnostics;

    /// <inheritdoc />
    public override void CustomInitialize() => BuildObjects();

    /// <summary>
    /// Builds every object in <see cref="Save"/>, attaching those authored to attach. Called by
    /// <see cref="CustomInitialize"/>; safe to call directly in tests.
    /// </summary>
    public void BuildObjects()
    {
        // See GlueScreen.BuildObjects — a rebuild must not leave the previous children attached.
        foreach (var previous in GlueElementBuilder.Flatten(_objects.Values))
        {
            if (previous is IAttachable attachable)
                Remove(attachable);
        }

        _objects.Clear();
        _buildDiagnostics.Clear();

        if (Save is null)
            return;

        GlueElementBuilder.Build(Save.NamedObjects, Save.Name, _objects, _buildDiagnostics,
            addSingle: (builder, save) => builder.AddTo(this, save, Save.Name));
    }

    /// <inheritdoc />
    public override string ToString() => GlueName ?? nameof(GlueEntity);
}
