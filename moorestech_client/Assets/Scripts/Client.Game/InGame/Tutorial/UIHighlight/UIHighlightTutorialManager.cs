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

            // anchorId無変換で渡す
            // Pass the anchorId verbatim
            return TutorialPresentationStateStore.Instance.AddOutlineHighlight(highlightParam.HighLightAnchorId, highlightParam.HighLightText, tutorial.TutorialGuid);
        }
    }
}
