using System;
using MainGame.UnityView.UI.Inventory.Element;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class InventoryItemSlot: MonoBehaviour
    {
        public event Action<InventoryItemSlot> OnRightClick;
        public event Action<InventoryItemSlot> OnLeftClick;

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

        
        
        
        
        
        
        
        
        
        /// <summary>
        /// これより下削除予定
        /// </summary>
        public delegate void OnItemSlotClicked(int slotIndex);
        public void SubscribeOnItemSlotClick(OnItemSlotClicked onItemSlotClicked) { }
        public void Construct(int slotIndex) { }
    }
}