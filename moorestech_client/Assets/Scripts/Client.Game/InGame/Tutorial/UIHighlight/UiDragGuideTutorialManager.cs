using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class UiDragGuideTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var param = (UiDragGuideTutorialParam)tutorial.TutorialParam;

            // D&DガイドはWebオーバーレイの矢印ループのみで表示する
            // The drag guide is rendered exclusively via the web overlay's looping arrow
            if (!TutorialAnchorIdMapper.TryFromUiObjectId(param.FromUIObjectId, out var fromAnchorId))
                return null;
            if (!TutorialAnchorIdMapper.TryFromUiObjectId(param.ToUIObjectId, out var toAnchorId))
                return null;
            return TutorialPresentationStateStore.Instance.AddDragGuide(fromAnchorId, toAnchorId);
        }
    }
}
