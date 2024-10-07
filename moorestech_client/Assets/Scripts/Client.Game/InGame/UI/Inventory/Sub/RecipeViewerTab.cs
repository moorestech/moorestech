using System;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    public class RecipeViewerTab : MonoBehaviour
    {
        [SerializeField] private GameObject selectedTab;
        [SerializeField] private GameObject unselectedTab;
        
        [SerializeField] private Sprite craftIcon;
        
        private RectTransform _rectTransform;
        
        public void Initialize()
        {
            _rectTransform = GetComponent<RectTransform>();
        }
        
        public void SetSelected(bool selected)
        {
            selectedTab.SetActive(selected);
            unselectedTab.SetActive(!selected);
        }
    }
}