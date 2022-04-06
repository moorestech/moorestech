using System;
using MainGame.UnityView.UI.Inventory.Element;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class InventoryItemSlot: MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler
    {
        private const string EmptyItemName = "EmptyItem";
        
        public delegate void OnItemSlotClicked(int slotIndex);

        [SerializeField] private Image image;
        public Image Image => image;

        
        [SerializeField] private TMP_Text countText;
        public TMP_Text CountText => countText;

        
        [SerializeField] private ItemNameText itemNameText;
        
        
        private int _slotIndex = -1;
        private string _itemName = EmptyItemName;
        private event OnItemSlotClicked ItemSlotClickedEvent;
        
        public void Construct(int slotIndex)
        {
            _slotIndex = slotIndex;
        }
        
        public void SetItem(ItemViewData itemView, int count)
        {
            image.sprite = itemView.itemImage;
            itemNameText.SetText(itemView.itemName);
            _itemName = itemView.itemName;
            
            if (count == 0)
            {
                countText.text = "";
            }
            else
            {
                countText.text = count.ToString();
            }
        }

        public void SubscribeOnItemSlotClick(OnItemSlotClicked onItemSlotClicked)
        {
            ItemSlotClickedEvent += onItemSlotClicked;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.pointerId == 2)
            {
                ItemSlotClickedEvent?.Invoke(_slotIndex);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EmptyItemName != _itemName)
            {
                itemNameText.SetActive(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData) { itemNameText.SetActive(false); }

        private void OnDisable() { itemNameText.SetActive(false); }
    }
}