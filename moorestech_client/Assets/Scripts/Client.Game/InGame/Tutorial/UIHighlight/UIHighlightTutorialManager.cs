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

            // マスタのanchorIdを無変換でWebオーバーレイへ渡す。文言があるときだけラベル用guidを添える
            // Pass the master anchorId verbatim; attach the label GUID only when the master has text
            var labelTutorialGuid = string.IsNullOrEmpty(highlightParam.HighLightText) ? null : tutorial.TutorialGuid.ToString();
            return TutorialPresentationStateStore.Instance.AddOutlineHighlight(highlightParam.HighLightAnchorId, labelTutorialGuid);
        }
    }
}
