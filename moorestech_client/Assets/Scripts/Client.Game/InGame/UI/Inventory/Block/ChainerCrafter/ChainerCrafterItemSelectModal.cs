using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Master;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block.ChainerCrafter
{
    public class ChainerCrafterItemSelectModal : MonoBehaviour
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        [SerializeField] private RectTransform itemsParent;
        [SerializeField] private TMP_InputField countInputField;
        
        [SerializeField] private Button okButton;
        
        private readonly List<ItemSlotObject> _itemSlotObjects = new();
        
        private ItemId _selectedItemId;
        
        public async UniTask<(ItemId,int)> Initialize(ItemId currentItemId, int currentCount)
        {
            _selectedItemId = currentItemId;
            // アイテムリストを初期化
            // Initialize item list
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);
                var slotObject = Instantiate(itemSlotObjectPrefab, itemsParent);
                slotObject.OnLeftClickUp.Subscribe(ClickItem);
                slotObject.SetItem(itemView, 0);
                _itemSlotObjects.Add(slotObject);
                
                if (itemId == currentItemId)
                {
                    slotObject.SetHotBarSelect(true);
                }
            }
            
            if (currentItemId == ItemMaster.EmptyItemId)
            {
                okButton.interactable = false;
            }
            countInputField.text = currentCount.ToString();
            
            await okButton.OnClickAsync();
            
            return (_selectedItemId, int.Parse(countInputField.text));
        }
        
        private void ClickItem(ItemSlotObject itemSlotObject)
        {
            foreach (var slotObject in _itemSlotObjects)
            {
                slotObject.SetHotBarSelect(false);
            }
            
            itemSlotObject.SetHotBarSelect(true);
            _selectedItemId = itemSlotObject.ItemViewData.ItemId;
            okButton.interactable = true;
        }
    }
}