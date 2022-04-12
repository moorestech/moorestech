using System;
using MainGame.UnityView.UI.Inventory.Element;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class InventoryItemSlot: MonoBehaviour,IPointerDownHandler
    {
        public event Action<InventoryItemSlot> OnRightClickDown;
        public event Action<InventoryItemSlot> OnLeftClickDown;

        [SerializeField] private Image image;
        [SerializeField] private TMP_Text countText;

        
        public void SetItem(ItemViewData itemView, int count)
        {
            image.sprite = itemView.itemImage;
            
            if (count == 0)
            {
                countText.text = "";
            }
            else
            {
                countText.text = count.ToString();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            switch (eventData.pointerId)
            {
                case -1:
                    OnLeftClickDown?.Invoke(this);
                    break;
                case -2:
                    OnRightClickDown?.Invoke(this);
                    break;
            }
        }
        
        
        
        
        
        
        
        /// <summary>
        /// これより下削除予定
        /// </summary>
        public delegate void OnItemSlotClicked(int slotIndex);
        public void SubscribeOnItemSlotClick(OnItemSlotClicked onItemSlotClicked) { }
        public void Construct(int slotIndex) { }
    }
}