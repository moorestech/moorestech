using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.UI.ContextMenu
{
    public class ContextMenuView : MonoBehaviour
    {
        [SerializeField] private ContextMenuViewItem contextMenuViewItemPrefab;
        [SerializeField] private Transform menuBarParent;
        
        public static ContextMenuView Instance { get; private set; }
        
        private List<ContextMenuBarInfo> _currentBars = new();
        
        private void Awake()
        {
            Instance = this;
        }
        
        public void Show(List<ContextMenuBarInfo> contextMenuBars)
        {
            gameObject.SetActive(true);
            _currentBars = contextMenuBars;
            foreach (var contextMenuBar in contextMenuBars)
            {
                var item = Instantiate(contextMenuViewItemPrefab, menuBarParent);
                item.Initialize(contextMenuBar);
            }
        }
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}