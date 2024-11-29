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
        }
        
        public async UniTask<(ItemId,int)> GetSelectItem(ItemId currentItemId, int currentCount)
        {
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
                
                return (_selectedItemId, int.Parse(countInputField.text));
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
            okButton.interactable = true;
        }
    }
}