using System.Text.Json.Serialization;

namespace FlatRedBall2.Glue.Model;

/// <summary>The contents of one Glue <c>.glej</c> file.</summary>
public class EntitySave : GlueElement
{
    /// <summary>
    /// The entity this one derives from, in the same backslash form as <see cref="GlueElement.Name"/>.
    /// Phase 1 retains it without merging; resolution is Phase 6.
    /// </summary>
    public string? BaseEntity { get; set; }

    /// <summary>Whether a factory is generated so other entities can spawn this one. Phase 8.</summary>
    public bool CreatedByOtherEntities { get; set; }

    /// <summary>Whether the factory pools instances rather than allocating per spawn. Phase 8.</summary>
    public bool PooledByFactory { get; set; }

    /// <summary>Whether this entity participates in collision. Phase 9.</summary>
    public bool ImplementsICollidable { get; set; }

    /// <summary>Whether this entity handles clicks.</summary>
    public bool ImplementsIClickable { get; set; }

    /// <summary>Whether this entity exposes visibility.</summary>
    public bool ImplementsIVisible { get; set; }

    /// <summary>Whether this entity carries Tiled tile metadata. Phase 10.</summary>
    public bool ImplementsITiledTileMetadata { get; set; }

    /// <summary>Whether the entity is treated as 2D.</summary>
    public bool Is2D { get; set; }

    /// <summary>
    /// Which input device drives this entity, when it has movement behavior. Stored in
    /// <see cref="GlueElement.Properties"/> rather than as its own JSON member. Mapped in Phase 11.
    /// </summary>
    [JsonIgnore]
    public int InputDevice => Properties.GetValue<int>(nameof(InputDevice));
}
