using System;
using Client.Mod.Texture;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    public class RecipeViewerTab : MonoBehaviour
    {
        [SerializeField] private RectTransform selectedTab;
        [SerializeField] private RectTransform unselectedTab;
        [SerializeField] private Image selectedIcon;
        [SerializeField] private Image unselectedIcon;
        
        [SerializeField] private Sprite craftIcon;
        
        private RectTransform _rectTransform;
        
        public void Initialize()
        {
            _rectTransform = GetComponent<RectTransform>();
        }
        
        public void SetSelected(bool selected)
        {
            selectedTab.gameObject.SetActive(selected);
            unselectedTab.gameObject.SetActive(!selected);
            var width = selected ? selectedTab.rect.width : unselectedTab.rect.width;
            
            _rectTransform.sizeDelta = new Vector2(width, _rectTransform.sizeDelta.y);
        }
        
        public void SetMachineItem(ItemViewData itemViewData)
        {
            selectedIcon.sprite = itemViewData.ItemImage;
            unselectedIcon.sprite = itemViewData.ItemImage;
        }
        
        public void SetCraftIcon()
        {
            selectedIcon.sprite = craftIcon;
            unselectedIcon.sprite = craftIcon;
        }
    }
}