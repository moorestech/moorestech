using System;
using Client.Mod.Texture;
using Core.Master;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.RecipeViewer
{
    public class RecipeViewerTabElement : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private RectTransform selectedTab;
        [SerializeField] private RectTransform unselectedTab;
        [SerializeField] private Image selectedIcon;
        [SerializeField] private Image unselectedIcon;
        
        [SerializeField] private Sprite craftIcon;
        
        public IObservable<BlockId?> OnClickTab => onClickTab; // nullならCraftを選択したことを意味する
        private readonly Subject<BlockId?> onClickTab = new(); // If null, it means that Craft is selected
        
        private RectTransform _rectTransform;
        private BlockId? _currentBlockId;
        
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
        
        public void SetMachineItem(BlockId blockId, ItemViewData itemViewData)
        {
            _currentBlockId = blockId;
            selectedIcon.sprite = itemViewData.ItemImage;
            unselectedIcon.sprite = itemViewData.ItemImage;
            
        }
        
        public void SetCraftIcon()
        {
            _currentBlockId = null;
            selectedIcon.sprite = craftIcon;
            unselectedIcon.sprite = craftIcon;
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            onClickTab.OnNext(_currentBlockId);
        }
    }
}