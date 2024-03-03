using System;
using System.Collections.Generic;
using Client.Game.Context;
using Core.Item;
using Core.Item.Config;
using Game.Block.Interface.RecipeConfig;
using Game.Crafting.Interface;
using MainGame.UnityView.Item;
using MainGame.UnityView.UI.Inventory.Element;
using UniRx;
using UnityEngine;

namespace MainGame.UnityView.UI.ItemRecipeViewer
{
    public class RecipeViewerList : MonoBehaviour
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform itemListView;
        [SerializeField] private RectTransform itemListParent;

        public IObservable<List<IRecipeViewerRecipe>> OnClickItem => _onClickItem;
        private readonly Subject<List<IRecipeViewerRecipe>> _onClickItem = new();

        private readonly List<ItemSlotObject> _itemSlotObjects = new();
        private IItemConfig _itemConfig;

        public void Initialize(IItemConfig itemConfig,ICraftingConfig craftConfig,IMachineRecipeConfig machineRecipeConfig)
        {
            _itemConfig = itemConfig;
            
            foreach (var item in itemConfig.ItemConfigDataList)
            {
                var itemViewData = MoorestechContext.ItemImageContainer.GetItemView(item.ItemId);
                
                var itemSlotObject = Instantiate(itemSlotObjectPrefab, itemListParent);
                itemSlotObject.SetItem(itemViewData, 0);
                
                itemSlotObject.OnLeftClickUp.Subscribe(OnClickItemList);
                _itemSlotObjects.Add(itemSlotObject);
            }
        }

        private void OnClickItemList(ItemSlotObject slotObject)
        {
            
        }

        public void DisplayCraftableItem(List<IItemStack> inventoryItems)
        {
            
        }
                
        public void DisplayCraftableItem(List<IItemStack> inventoryItems,int blockId)
        {
            
        }

        
    }

    public interface IRecipeViewerRecipe
    {
        
    }
}