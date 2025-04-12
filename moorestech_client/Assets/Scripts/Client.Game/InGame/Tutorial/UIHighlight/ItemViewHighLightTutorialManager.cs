using Client.Game.InGame.UI.Inventory.Sub;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

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
            return UIHighlightTutorialManager.SetHighLightTargetObject(highlightTutorialViewPrefab, highlightParent, itemViewObjectId, text);
        }
    }
}