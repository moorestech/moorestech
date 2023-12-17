using System;
using Core.Item.Config;
using Game.Crafting.Interface;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.UIObjects;
using Server.Protocol.PacketResponse;
using SinglePlay;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.CraftSub
{
    public class CraftInventoryView : MonoBehaviour
    {
        [SerializeField] private UIBuilderItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform craftMaterialParent;
        [SerializeField] private RectTransform craftResultParent;
        [SerializeField] private RectTransform itemListParent;
        
        [SerializeField] private Button craftButton;
        [SerializeField] private Button nextRecipeButton;
        [SerializeField] private Button prevRecipeButton;
        
        private IItemConfig _itemConfig;
        private ICraftingConfig _craftingConfig;
        private ItemImageContainer _itemImageContainer;
        


        [Inject]
        public void Construct(SinglePlayInterface singlePlay,ItemImageContainer itemImageContainer)
        {
            _itemConfig = singlePlay.ItemConfig;
            _craftingConfig = singlePlay.CraftingConfig;
            _itemImageContainer = itemImageContainer;

            foreach (var item in _itemConfig.ItemConfigDataList)
            {
                var itemViewData = _itemImageContainer.GetItemView(item.ItemId);
                
                var itemSlotObject = Instantiate(itemSlotObjectPrefab, itemListParent);
                itemSlotObject.SetItem(itemViewData, 0,false);
                itemSlotObject.OnLeftClickUp.Subscribe(OnClickItemList);
            }
        }

        private void OnClickItemList(UIBuilderItemSlotObject slot)
        {
        }

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
        
    }
}