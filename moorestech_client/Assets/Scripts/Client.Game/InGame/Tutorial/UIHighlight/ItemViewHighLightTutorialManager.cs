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
            var itemViewObjectId = string.Format(ItemListView.ItemRecipeListHighlightKey, highlightParam.HighLightItemGuid);
            
            // ハイライトを設定
            var highlightTargetObjects = FindObjectsOfType<UIHighlightTutorialTargetObject>(true);
            foreach (var targetObject in highlightTargetObjects)
            {
                if (targetObject.HighlightObjectId != itemViewObjectId) continue;
                
                var highlightView = Instantiate(highlightTutorialViewPrefab, transform);
                highlightView.SetTargetObject(targetObject, highlightParam.HighLightText);
                return highlightView;
            }
            
            return null;
        }
    }
}