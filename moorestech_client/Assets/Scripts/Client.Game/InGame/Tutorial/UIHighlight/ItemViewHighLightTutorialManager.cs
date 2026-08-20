using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using Client.Game.InGame.UI.UIState;
using Core.Master;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class ItemViewHighLightTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var highlightParam = (ItemViewHighLightTutorialParam)tutorial.TutorialParam;

            // アイテムハイライトもWebオーバーレイのDOMハイライトのみで表示する
            // Item highlighting is rendered exclusively via the web overlay's DOM highlight
            var itemId = MasterHolder.ItemMaster.GetItemId(highlightParam.HighLightItemGuid).AsPrimitive();
            var anchorId = TutorialAnchorIdMapper.FromItemId(itemId);
            var labelTutorialGuid = string.IsNullOrEmpty(highlightParam.HighLightText) ? null : tutorial.TutorialGuid.ToString();
            return TutorialPresentationStateStore.Instance.AddOutlineHighlight(anchorId, labelTutorialGuid);
        }
    }
}
