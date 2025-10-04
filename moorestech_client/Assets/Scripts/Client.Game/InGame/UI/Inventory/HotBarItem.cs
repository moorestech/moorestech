using Client.Game.InGame.UI.Inventory.Common;
using Client.Mod.Texture;
using Core.Master;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory
{
    public class HotBarItem : MonoBehaviour
    {
        public ItemId ItemId => itemSlotView.ItemViewData?.ItemId ?? ItemMaster.EmptyItemId;
        
        [SerializeField] private ItemSlotView itemSlotView;
        [SerializeField] private TMP_Text keyBoardText;
        
        private void Awake()
        {
        }
        
        public void SetItem(ItemViewData itemViewData, int count)
        {
            itemSlotView.SetItem(itemViewData, count);
        }
        
        public void SetKeyBoardText(string text)
        {
            keyBoardText.text = text;
        }
        
        public void SetSelect(bool isSelect)
        {
            itemSlotView.SetHotBarSelected(isSelect);
        }
    }
}