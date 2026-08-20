using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using Client.Game.InGame.UI.UIState;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class UIHighlightTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var highlightParam = (UiHighLightTutorialParam)tutorial.TutorialParam;

            // マスタのanchorIdを無変換でWebオーバーレイへ渡す。DOMとの突き合わせはWeb側のみが行う
            // Pass the master anchorId to the web overlay verbatim; DOM matching happens only on the web side
            return TutorialPresentationStateStore.Instance.AddOutlineHighlight(highlightParam.HighLightAnchorId);
        }
    }
}
