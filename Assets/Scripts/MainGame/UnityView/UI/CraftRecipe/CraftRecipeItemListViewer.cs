using System.Collections.Generic;
using MainGame.Basic.UI;
using MainGame.UnityView.UI.Builder.Unity;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class CraftRecipeItemListViewer : MonoBehaviour
    {
        [SerializeField] private UIBuilderItemSlotObject UIBuilderItemSlotObjectPrefab;
        

        public delegate void ItemSlotClick(int itemId);
        public event ItemSlotClick OnItemListClick;
        
        private readonly Dictionary<UIBuilderItemSlotObject, int> _uiObjectToItemId = new();
        private readonly Dictionary<int, UIBuilderItemSlotObject> _itemIdToUiObject = new();
        
        public bool IsUIActive => gameObject.activeSelf;


        [Inject]
        public void Construct(ItemImages itemImages)
        {
            itemImages.OnLoadFinished += () => CreateItemLabel(itemImages);
        }

        private void CreateItemLabel(ItemImages itemImages)
        {
            //ブロックのIDは1から始まるので+1しておく
            for (int i = 1; i < itemImages.GetItemNum() + 1; i++)
            {
                var g = Instantiate(UIBuilderItemSlotObjectPrefab, transform, true);
                g.SetItem(itemImages.GetItemView(i),0);
                
                _uiObjectToItemId.Add(g,i);
                _itemIdToUiObject.Add(i,g);

                g.transform.localScale = new Vector3(1,1,1);

                
                g.OnLeftClickDown += itemSlot => OnItemListClick?.Invoke(_uiObjectToItemId[itemSlot]);
            }
        }
        
        public RectTransformReadonlyData GetRectTransformData(int itemId)
        {
            return _itemIdToUiObject[itemId].GetRectTransformData();
        }
    }
}