using System;
using System.Collections.Generic;
using Core.Item.Config;
using MainGame.UnityView.UI.CraftRecipe;
using SinglePlay;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Tutorial
{
    public class HighlightRecipeViewerItem : MonoBehaviour
    {
        private IItemConfig _itemConfig;
        private readonly Dictionary<int, RectTransformHighlightObject> _rectTransformHighlightObjects = new ();

        [SerializeField] private CraftRecipeItemListViewer craftRecipeItemListViewer;
        [SerializeField] private RectTransformHighlight rectTransformHighlight;
        
        [Inject]
        public void Inject(SinglePlayInterface singlePlayInterface)
        {
            _itemConfig = singlePlayInterface.ItemConfig;
        }

        public void SetHighLight(int itemId,bool enable)
        {
            // TODO ここがmodのロード前に呼び出されるとバグるので修正する そもそもここが動く時データがロードされていないのが問題であるので、設計を変更する必要がある
            var isExist = _rectTransformHighlightObjects.TryGetValue(itemId, out var highlightObject);
            
            //ハイライトがない場合でオンにする場合は作成
            if (!isExist && enable)
            {
                var rectData = craftRecipeItemListViewer.GetRectTransformData(itemId);
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

        /// <summary>
        /// アイテム一覧は生成時と一番最初にアイテムリストを開いたときでや
        /// スクロールなどで位置が変わるなどがあるので、毎フレームアップデートして位置を同期させる
        /// </summary>
        private void Update()
        {
            foreach (var retTransformObjects in _rectTransformHighlightObjects.Values)
            {
                retTransformObjects.UpdateObject();
            }
            
        }


        public void SetHighLight(string modId, string itemName,bool enable)
        {
            SetHighLight(_itemConfig.GetItemId(modId,itemName),enable);
        }

    }
}