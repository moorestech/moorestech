using System;
using System.Collections.Generic;
using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class CraftRecipeItemListViewer : MonoBehaviour
    {
        [SerializeField] private InventoryItemSlot inventoryItemSlotPrefab;
        

        public delegate void ItemSlotClick(int itemId);
        public event ItemSlotClick OnItemListClick;
        
        public event Action<int> OnCursorEnter;
        public event Action<int> OnCursorExit;
        
        private readonly Dictionary<InventoryItemSlot, int> _itemIdTable = new();


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
                var g = Instantiate(inventoryItemSlotPrefab, transform, true);
                g.SetItem(itemImages.GetItemView(i),0);
                _itemIdTable.Add(g,i);

                g.transform.localScale = new Vector3(1,1,1);

                
                g.OnLeftClickDown += itemSlot => OnItemListClick?.Invoke(_itemIdTable[itemSlot]);
                g.OnCursorEnter += itemSlot => OnCursorEnter?.Invoke(_itemIdTable[itemSlot]);
                g.OnCursorExit += itemSlot => OnCursorExit?.Invoke(_itemIdTable[itemSlot]);
            }
        }
    }
}