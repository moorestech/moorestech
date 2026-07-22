using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using Client.Game.InGame.UI.UIState;
using Core.Master;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class ItemViewHighLightTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public ITutorialView ApplyTutorial(ITutorialParam param)
        {
            var highlightParam = (ItemViewHighLightTutorialParam)param;

            // アイテムハイライトもWebオーバーレイのDOMハイライトのみで表示する
            // Item highlighting is rendered exclusively via the web overlay's DOM highlight
            var itemId = MasterHolder.ItemMaster.GetItemId(highlightParam.HighLightItemGuid).AsPrimitive();
            var anchorId = TutorialAnchorIdMapper.FromItemId(itemId);
            return TutorialPresentationStateStore.Instance.AddHighlight(anchorId, "spotlight", highlightParam.HighLightText);
        }
    }
}
