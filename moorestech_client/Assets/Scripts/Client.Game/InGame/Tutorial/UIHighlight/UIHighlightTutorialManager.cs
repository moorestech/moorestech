using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using Client.Game.InGame.UI.UIState;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class UIHighlightTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public string TutorialType => TutorialsElement.TutorialTypeConst.uiHighLight;

        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var highlightParam = (UiHighLightTutorialParam)tutorial.TutorialParam;

            // マスタのanchorIdは無変換で渡す（DOM突き合わせはWeb側のみが担うため、ここで変換・検証しない）
            // Pass the master anchorId verbatim; only the web side matches it against the DOM, so no mapping or validation here
            return TutorialPresentationStateStore.Instance.AddOutlineHighlight(highlightParam.HighLightAnchorId, tutorial.TutorialGuid);
        }
    }
}
