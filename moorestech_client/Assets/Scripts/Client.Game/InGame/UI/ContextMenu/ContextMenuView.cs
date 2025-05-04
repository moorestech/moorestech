using System.Collections.Generic;
using Client.Game.InGame.Control;
using UnityEngine;

namespace Client.Game.InGame.UI.ContextMenu
{
    public class ContextMenuView : MonoBehaviour
    {
        [SerializeField] private ContextMenuViewItem contextMenuViewItemPrefab;
        [SerializeField] private Transform menuParent;
        [SerializeField] private Transform menuBarContent;
        
        public static ContextMenuView Instance { get; private set; }
        
        private UICursorFollowControlRootCanvasRect _canvasRectRoot;
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
                var item = Instantiate(contextMenuViewItemPrefab, menuBarContent);
                item.Initialize(contextMenuBar);
            }
            
            if (_canvasRectRoot == null)
            {
                _canvasRectRoot = FindObjectOfType<UICursorFollowControlRootCanvasRect>();
                if (_canvasRectRoot == null) return;
            }
            
            menuParent.transform.localPosition = UICursorFollowControl.GetLocalPosition(_canvasRectRoot, transform.localPosition, Vector3.zero);
        }
        
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}