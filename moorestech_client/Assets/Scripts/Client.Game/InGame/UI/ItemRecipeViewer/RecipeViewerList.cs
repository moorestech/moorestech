using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Item.Config;
using Core.Item.Interface;
using Core.Item.Interface.Config;
using Game.Block.Interface.RecipeConfig;
using Game.Crafting.Interface;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.ItemRecipeViewer
{
    public class RecipeViewerList : MonoBehaviour
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;

        [SerializeField] private RectTransform itemListView;
        [SerializeField] private RectTransform itemListParent;

        private readonly List<ItemSlotObject> _itemSlotObjects = new();
        private readonly Subject<List<IRecipeViewerRecipe>> _onClickItem = new();
        private IItemConfig _itemConfig;

        public IObservable<List<IRecipeViewerRecipe>> OnClickItem => _onClickItem;

        public void Initialize(IItemConfig itemConfig, ICraftingConfig craftConfig, IMachineRecipeConfig machineRecipeConfig)
        {
            _itemConfig = itemConfig;

            foreach (var item in itemConfig.ItemConfigDataList)
            {
                var itemViewData = ClientContext.ItemImageContainer.GetItemView(item.ItemId);

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

        public void DisplayCraftableItem(List<IItemStack> inventoryItems, int blockId)
        {
        }
    }

    public interface IRecipeViewerRecipe
    {
    }
}