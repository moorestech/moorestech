using Client.Game.InGame.UI.Inventory.Craft;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using Client.Game.InGame.UI.UIState;
using Core.Master;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class ItemViewHighLightTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        [SerializeField] private UIHighlightTutorialView highlightTutorialViewPrefab;
        [SerializeField] private RectTransform highlightParent;
        
        public ITutorialView ApplyTutorial(ITutorialParam param)
        {
            var highlightParam = (ItemViewHighLightTutorialParam)param;
            
            // アイテムリストのハイライトオブジェクトIDを取得
            // Get the item list highlight object ID
            var itemViewObjectId = string.Format(ItemListView.ItemRecipeListHighlightKey, highlightParam.HighLightItemGuid);
            
            var text = highlightParam.HighLightText;
            if (WebUiScreenGate.IsWebUiMode)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(highlightParam.HighLightItemGuid).AsPrimitive();
                var anchorId = TutorialAnchorIdMapper.FromItemId(itemId);
                return TutorialPresentationStateStore.Instance.AddHighlight(anchorId, "spotlight", text);
            }
            const bool forceCreate = true; // アイテムビューのオブジェクトは必ずしもチュートリアル実行時に存在するとは限らないため強制作成する
            return UIHighlightTutorialManager.SetHighLightTargetObject(highlightTutorialViewPrefab, highlightParent, itemViewObjectId, text, forceCreate);
        }
    }
}
