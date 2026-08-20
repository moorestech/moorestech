using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class UiDragGuideTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var param = (UiDragGuideTutorialParam)tutorial.TutorialParam;

            // マスタのfrom/to anchorIdを無変換でWebオーバーレイへ渡す。DOMとの突き合わせはWeb側のみが行う
            // Pass the master from/to anchorIds to the web overlay verbatim; DOM matching happens only on the web side
            return TutorialPresentationStateStore.Instance.AddDragGuide(param.FromAnchorId, param.ToAnchorId);
        }
    }
}
