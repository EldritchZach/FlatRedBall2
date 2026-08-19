using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.IO;

public enum UvLoadOutcome
{
    LoadAsIs,
    ConvertAndLoad,
    RefuseUserDeclined,
    RefuseMissingTextures,
}

/// <summary>
/// Pure decision function for the UV-coordinate load gate. Callers gather the facts
/// (coordinate type, texture availability, user answer) and pass them in; this method
/// returns the outcome without performing any I/O or showing dialogs.
/// </summary>
public static class UvLoadGate
{
    public static UvLoadOutcome DecideOutcome(
        TextureCoordinateType coordinateType,
        bool allTexturesResolvable,
        bool userConfirmed)
    {
        if (coordinateType == TextureCoordinateType.Pixel)
            return UvLoadOutcome.LoadAsIs;

        if (!allTexturesResolvable)
            return UvLoadOutcome.RefuseMissingTextures;

        return userConfirmed ? UvLoadOutcome.ConvertAndLoad : UvLoadOutcome.RefuseUserDeclined;
    }
}
