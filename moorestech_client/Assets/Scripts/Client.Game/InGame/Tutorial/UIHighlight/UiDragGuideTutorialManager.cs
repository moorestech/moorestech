using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class UiDragGuideTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public string TutorialType => TutorialsElement.TutorialTypeConst.uiDragGuide;

        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var param = (UiDragGuideTutorialParam)tutorial.TutorialParam;

            // マスタのfrom/to anchorIdを無変換でUITKオーバーレイへ渡す。要素との突き合わせはUITK側のみが行う
            // Pass the master from/to anchorIds to the UITK overlay verbatim; element matching happens only on the UITK side
            return TutorialPresentationStateStore.Instance.AddDragGuide(param.FromAnchorId, param.ToAnchorId);
        }
    }
}
