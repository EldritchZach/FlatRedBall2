using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core
{
    public interface IObjectFinder
    {
        AnimationFrameSave? GetAnimationFrameContaining(AARectSave rectangle);
        AnimationFrameSave? GetAnimationFrameContaining(CircleSave circle);
        AnimationChainSave? GetAnimationChainContaining(AnimationFrameSave frame);
    }
}
