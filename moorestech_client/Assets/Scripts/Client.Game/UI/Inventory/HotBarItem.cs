using System;
using MainGame.ModLoader.Texture;
using MainGame.UnityView.UI.Inventory.Element;
using TMPro;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory
{
    public class HotBarItem : MonoBehaviour
    {
        [SerializeField] private ItemSlotObject itemSlotObject;
        [SerializeField] private TMP_Text keyBoardText;

        private void Awake()
        {
            
        }

        public void SetItem(ItemViewData itemViewData, int count)
        {
            itemSlotObject.SetItem(itemViewData, count);
        }
        
        public void SetKeyBoardText(string text)
        {
            keyBoardText.text = text;
        }
        
        public void SetSelect(bool isSelect)
        {
            itemSlotObject.SetHotBarSelect(isSelect);
        }
    }
}