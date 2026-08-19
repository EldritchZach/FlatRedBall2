using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core;

/// <summary>
/// Tracks animation items cut to the clipboard that remain in the project until paste completes.
/// </summary>
public interface IPendingCutState
{
    event Action? Changed;

    bool IsActive { get; }
    CopySelectionKind? Kind { get; }
    IReadOnlyList<AnimationChainSave> Chains { get; }
    IReadOnlyList<AnimationFrameSave> Frames { get; }
    IReadOnlyList<object> Shapes { get; }

    void Set(CopySelectionPayload payload);
    void Clear();
    bool Contains(object data);

    /// <summary>Frames that should show a cut outline on the wireframe.</summary>
    IReadOnlyList<AnimationFrameSave> WireframeFrames { get; }

    /// <summary>Shapes that should show a cut outline in the preview panel.</summary>
    IReadOnlyList<object> WireframeShapes { get; }

    /// <summary>True when every pending-cut source still lives in <paramref name="acls"/>.</summary>
    bool SourcesBelongToProject(AnimationChainListSave? acls, IObjectFinder finder);
}

public sealed class PendingCutState : IPendingCutState
{
    private CopySelectionPayload? _payload;

    public event Action? Changed;

    public bool IsActive => _payload is not null;
    public CopySelectionKind? Kind => _payload?.Kind;
    public IReadOnlyList<AnimationChainSave> Chains => _payload?.Chains ?? [];
    public IReadOnlyList<AnimationFrameSave> Frames => _payload?.Frames ?? [];
    public IReadOnlyList<object> Shapes => _payload?.Shapes ?? [];

    public IReadOnlyList<AnimationFrameSave> WireframeFrames =>
        _payload?.Kind switch
        {
            CopySelectionKind.Chain => _payload.Chains.SelectMany(c => c.Frames).ToList(),
            CopySelectionKind.Frame => _payload.Frames,
            _ => [],
        };

    public IReadOnlyList<object> WireframeShapes =>
        _payload?.Kind == CopySelectionKind.Shape ? _payload.Shapes : [];

    public void Set(CopySelectionPayload payload)
    {
        _payload = payload;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (_payload is null) return;
        _payload = null;
        Changed?.Invoke();
    }

    public bool Contains(object data) =>
        _payload?.Kind switch
        {
            CopySelectionKind.Chain => data is AnimationChainSave c && _payload.Chains.Contains(c),
            CopySelectionKind.Frame => data is AnimationFrameSave f && _payload.Frames.Contains(f),
            CopySelectionKind.Shape => _payload.Shapes.Contains(data),
            _ => false,
        };

    public bool SourcesBelongToProject(AnimationChainListSave? acls, IObjectFinder finder)
    {
        if (_payload is null || acls is null) return false;
        return _payload.Kind switch
        {
            CopySelectionKind.Chain => _payload.Chains.All(acls.AnimationChains.Contains),
            CopySelectionKind.Frame => _payload.Frames.All(f =>
                acls.AnimationChains.Any(c => c.Frames.Contains(f))),
            CopySelectionKind.Shape => _payload.Shapes.All(s => s switch
            {
                AARectSave r => BelongsToProject(finder.GetAnimationFrameContaining(r), acls),
                CircleSave c => BelongsToProject(finder.GetAnimationFrameContaining(c), acls),
                _ => false,
            }),
            _ => false,
        };
    }

    private static bool BelongsToProject(AnimationFrameSave? frame, AnimationChainListSave acls) =>
        frame is not null && acls.AnimationChains.Any(c => c.Frames.Contains(frame));
}
