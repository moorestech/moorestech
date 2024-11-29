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
    public class CraftChainerCrafterItemSelectModal : MonoBehaviour
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        [SerializeField] private RectTransform itemsParent;
        [SerializeField] private TMP_InputField countInputField;
        
        [SerializeField] private Button okButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button clearButton;
        
        private readonly List<ItemSlotObject> _itemSlotObjects = new();
        
        private ItemId _selectedItemId;
        
        public void Initialize()
        {
            gameObject.SetActive(false);
            // アイテムリストを初期化
            // Initialize item list
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);
                var slotObject = Instantiate(itemSlotObjectPrefab, itemsParent);
                slotObject.OnLeftClickUp.Subscribe(ClickItem);
                slotObject.SetItem(itemView, 0);
                _itemSlotObjects.Add(slotObject);
            }
            
            countInputField.onValueChanged.AddListener(UpdateOkButton);
        }
        
        public async UniTask<(ItemId,int)> GetSelectItem(ItemId currentItemId, int currentCount)
        {
            currentCount = Mathf.Max(1, currentCount);
            _selectedItemId = currentItemId;
            SetupUI();
            
            var result = await WaitPushButton();
            
            gameObject.SetActive(false);
            return result;
            
            #region Internal
            
            void SetupUI()
            {
                gameObject.SetActive(true);
                
                foreach (var slotObject in _itemSlotObjects)
                {
                    slotObject.SetHotBarSelect(false);
                    if (slotObject.ItemViewData.ItemId == currentItemId)
                    {
                        slotObject.SetHotBarSelect(true);
                    }
                }
                
                okButton.interactable = currentItemId != ItemMaster.EmptyItemId;
                countInputField.text = currentCount.ToString();
            }
            
            async UniTask<(ItemId, int)> WaitPushButton()
            {
                var ok = okButton.OnClickAsync();
                var cancel = cancelButton.OnClickAsync();
                var clear = clearButton.OnClickAsync();
                await UniTask.WhenAny(ok, cancel, clear);
                
                if (cancel.Status == UniTaskStatus.Succeeded)
                {
                    return (currentItemId, currentCount);
                }
                if (clear.Status == UniTaskStatus.Succeeded)
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
                slotObject.SetHotBarSelect(false);
            }
            
            itemSlotObject.SetHotBarSelect(true);
            _selectedItemId = itemSlotObject.ItemViewData.ItemId;
            
            UpdateOkButton(countInputField.text);
        }
        
        private void UpdateOkButton(string inputFieldText)
        {
            if (int.TryParse(inputFieldText, out var count))
            {
                okButton.interactable = 0 < count;
                return;
            }
            okButton.interactable = false;
        }
    }
}