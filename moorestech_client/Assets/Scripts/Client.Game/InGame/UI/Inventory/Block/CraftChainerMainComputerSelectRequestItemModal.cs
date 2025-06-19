using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class CraftChainerMainComputerSelectRequestItemModal : MonoBehaviour
    {
        [SerializeField] private RectTransform itemsParent;
        [SerializeField] private TMP_InputField countInputField;
        
        [SerializeField] private Button requestButton;
        [SerializeField] private Button cancelButton;
        
        private readonly List<ItemSlotObject> _itemSlotObjects = new();
        private ItemId _selectedItemId;
        
        public void Initialize()
        {
            gameObject.SetActive(false);
            // Initialize item list
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);
                var slotObject = Instantiate(ItemSlotObject.Prefab, itemsParent);
                slotObject.SetItem(itemView, 0);
                slotObject.OnLeftClickUp.Subscribe(ClickItem);
                _itemSlotObjects.Add(slotObject);
            }
            countInputField.onValueChanged.AddListener(UpdateRequestButton);
        }
        
        public async UniTask<(ItemId,int)> GetRequestItem()
        {
            SetupUI();
            
            var result = await WaitRequestButton();
            
            gameObject.SetActive(false);
            return result;
            
            #region Internal
            
            void SetupUI()
            {
                gameObject.SetActive(true);
                countInputField.text = 1.ToString();
            }
            
            async UniTask<(ItemId, int)> WaitRequestButton()
            {
                var request = requestButton.OnClickAsync();
                var cancel = cancelButton.OnClickAsync();
                await UniTask.WhenAny(request, cancel);
                
                if (cancel.Status == UniTaskStatus.Succeeded)
                {
                    return (ItemMaster.EmptyItemId, 0);
                }
                
                if (int.TryParse(countInputField.text, out var count))
                {
                    return (_selectedItemId, count);
                }
                
                return (_selectedItemId, 1);
            }
            
            #endregion
        }
        
        
        private void ClickItem(ItemSlotObject itemSlotObject)
        {
            foreach (var slotObject in _itemSlotObjects)
            {
                slotObject.SetHotBarSelected(false);
            }
            
            itemSlotObject.SetHotBarSelected(true);
            _selectedItemId = itemSlotObject.ItemViewData.ItemId;
            
            UpdateRequestButton(countInputField.text);
        }
        
        private void UpdateRequestButton(string inputFieldText)
        {
            if (int.TryParse(inputFieldText, out var count))
            {
                requestButton.interactable = 0 < count;
                return;
            }
            requestButton.interactable = false;
        }
    }
}