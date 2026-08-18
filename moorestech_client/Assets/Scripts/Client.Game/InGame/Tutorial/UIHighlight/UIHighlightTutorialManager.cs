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

            // UIHighlightはWebオーバーレイのDOMハイライトのみで表示する
            // UI highlighting is rendered exclusively via the web overlay's DOM highlight
            if (!TutorialAnchorIdMapper.TryFromUiObjectId(highlightParam.HighLightUIObjectId, out var anchorId))
                return null;
            return TutorialPresentationStateStore.Instance.AddOutlineHighlight(anchorId);
        }
    }
}
