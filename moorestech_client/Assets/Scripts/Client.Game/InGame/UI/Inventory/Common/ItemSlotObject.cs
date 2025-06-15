using Client.Mod.Texture;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Common
{
    public class ItemSlotObject : MonoBehaviour
    {
        [SerializeField] private CommonSlotView commonSlotView;
        
        public ItemViewData ItemViewData { get; private set; }
        public int Count { get; private set; }
        
        
        public void SetItem(ItemViewData itemView, int count, string toolTipText = null)
        {
            ItemViewData = itemView;
            Count = count;
            
            if (itemView == null || itemView.ItemId == ItemMaster.EmptyItemId)
            {
                commonSlotView.SetViewClear();
            }
            else
            {
                if (string.IsNullOrEmpty(toolTipText))
                {
                    toolTipText = GetToolTipText(itemView);
                }
                
                var countText = count != 0 ? count.ToString() : string.Empty;
                commonSlotView.SetView(itemView.ItemImage, countText, toolTipText);
            }
        }
        
        public void SetSlotViewOption(CommonSlotViewOption slotOption)
        {
            commonSlotView.SetSlotViewOption(slotOption);
        }
        
        public void SetActive(bool active)
        {
            commonSlotView.SetActive(active);
        }
        
        
        public static string GetToolTipText(ItemViewData itemView)
        {
            return $"{itemView.ItemName}";
        }
    }
}