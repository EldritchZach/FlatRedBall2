using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlatRedBall2.Glue.Model;

/// <summary>
/// A variable exposed on a Glue element. Phase 1 retains these; applying values is Phase 3.
/// </summary>
/// <remarks>
/// Most of what matters about a variable — including its declared type — lives in
/// <see cref="Properties"/> rather than in named JSON members, so the accessors below read through
/// the bag.
/// </remarks>
public class CustomVariable
{
    /// <summary>The variable's name as exposed on the element.</summary>
    public string? Name { get; set; }

    /// <summary>The authored default, left undecoded until Phase 3 knows the target type.</summary>
    public JsonElement DefaultValue { get; set; }

    /// <summary>The object this variable forwards to, when it tunnels into a member.</summary>
    public string? SourceObject { get; set; }

    /// <summary>The member on <see cref="SourceObject"/> this variable forwards to.</summary>
    public string? SourceObjectProperty { get; set; }

    /// <summary>Editor grouping. Carried for fidelity; not behavioral.</summary>
    public string? Category { get; set; }

    /// <summary>Whether derived elements may override this variable.</summary>
    public bool SetByDerived { get; set; }

    /// <summary>Glue's name/value bag. The accessors below are stored here, not as JSON members.</summary>
    public List<PropertySave> Properties { get; set; } = new();

    /// <summary>
    /// The variable's declared type, as Glue's own type string (<c>float</c>, <c>Color</c>, an enum
    /// name). Has no JSON member of its own — it is only ever stored in <see cref="Properties"/>.
    /// </summary>
    [JsonIgnore]
    public string? Type => Properties.GetValue<string>(nameof(Type));

    /// <summary>Visibility scope, stored in <see cref="Properties"/>.</summary>
    [JsonIgnore]
    public int Scope => Properties.GetValue<int>(nameof(Scope));

    /// <inheritdoc />
    public override string ToString() => Type is null ? Name ?? "" : $"{Type} {Name}";
}
