using System.Collections.Generic;
using Core.Item.Config;
using MainGame.UnityView.UI.CraftRecipe;
using SinglePlay;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Tutorial
{
    public class HighLightRecipeViewerItem : MonoBehaviour
    {
        private IItemConfig _itemConfig;
        private readonly Dictionary<int, RectTransformHighlightObject> _rectTransformHighlightObjects = new ();

        [SerializeField] private CraftRecipeItemListViewer itemListViewer;
        [SerializeField] private RectTransformHighlight rectTransformHighlight;
        
        [Inject]
        public void Inject(SinglePlayInterface singlePlayInterface)
        {
            _itemConfig = singlePlayInterface.ItemConfig;
        }

        public void SetHighLight(int itemId,bool enable)
        {
            var isExist = _rectTransformHighlightObjects.TryGetValue(itemId, out var highlightObject);
            
            //ハイライトがない場合でオンにする場合は作成
            if (!isExist && enable)
            {
                var rectData = itemListViewer.GetRectTransformData(itemId);
                _rectTransformHighlightObjects[itemId] = rectTransformHighlight.CreateHighlightObject(rectData);
                
                return;
            }

            //ハイライトがあって、オフにする場合は削除
            if (isExist && !enable)
            {
                highlightObject.Destroy();
                _rectTransformHighlightObjects.Remove(itemId);
                return;
            }

        }

        public void SetHighLight(string modId, string itemName,bool enable)
        {
            SetHighLight(_itemConfig.GetItemId(modId,itemName),enable);
        }
        
    }
}