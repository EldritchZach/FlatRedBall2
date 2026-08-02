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
/// <para>This phase builds an <em>empty</em> screen: the save's objects, variables, and files are
/// parsed and retained but nothing is constructed from them yet.</para>
/// </remarks>
public class GlueScreen : Screen
{
    /// <summary>
    /// The screen data this was built from. Assign it before <c>CustomInitialize</c> runs — the
    /// <c>configure</c> callback on <c>Start</c> and <c>MoveToScreen</c> is the intended place.
    /// </summary>
    public ScreenSave? Save { get; set; }

    /// <summary>The Glue element name, in backslash form (<c>Screens\Level1</c>).</summary>
    public string? GlueName => Save?.Name;

    /// <inheritdoc />
    public override string ToString() => GlueName ?? nameof(GlueScreen);
}

/// <summary>
/// An FRB2 <see cref="Entity"/> built from a Glue <see cref="EntitySave"/>. As with
/// <see cref="GlueScreen"/>, every loaded entity shares this one type and is distinguished by its
/// data.
/// </summary>
/// <remarks>
/// This phase builds an empty entity; constructing its objects is Phase 2 and applying its variables
/// is Phase 3.
/// </remarks>
public class GlueEntity : Entity
{
    /// <summary>The entity data this was built from. Assign it before <c>CustomInitialize</c> runs.</summary>
    public EntitySave? Save { get; set; }

    /// <summary>The Glue element name, in backslash form (<c>Entities\Player</c>).</summary>
    public string? GlueName => Save?.Name;

    /// <inheritdoc />
    public override string ToString() => GlueName ?? nameof(GlueEntity);
}
