using System.Collections.Generic;
using Client.Game.InGame.Control;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.ContextMenu
{
    public class ContextMenuView : MonoBehaviour
    {
        [SerializeField] private ContextMenuViewItem contextMenuViewItemPrefab;
        [SerializeField] private Transform menuParent;
        [SerializeField] private Transform menuBarContent;
        
        public static ContextMenuView Instance { get; private set; }
        
        private readonly List<ContextMenuViewItem> _contextMenuViewItems = new();
        private UICursorFollowControlRootCanvasRect _canvasRectRoot;
        private UGuiContextMenuTarget _contextMenuTarget;
        
        private void Awake()
        {
            Instance = this;
        }
        
        public void Show(UGuiContextMenuTarget currentTarget, List<ContextMenuBarInfo> contextMenuBars)
        {
            _contextMenuTarget = currentTarget;
            gameObject.SetActive(true);
            
            SetContextMenu();
            SetPosition();
            
            #region Internal
            
            void SetContextMenu()
            {
                foreach (var item in _contextMenuViewItems)
                {
                    Destroy(item.gameObject);
                }
                _contextMenuViewItems.Clear();
                
                foreach (var contextMenuBar in contextMenuBars)
                {
                    var item = Instantiate(contextMenuViewItemPrefab, menuBarContent);
                    item.Initialize(contextMenuBar);
                    item.OnPointerClick.Subscribe(_ =>
                    {
                        Hide();
                    });
                    _contextMenuViewItems.Add(item);
                }
            }
            
            void SetPosition()
            {
                if (_canvasRectRoot == null)
                {
                    _canvasRectRoot = FindObjectOfType<UICursorFollowControlRootCanvasRect>();
                    if (_canvasRectRoot == null) return;
                }
                
                menuParent.transform.localPosition = UICursorFollowControl.GetLocalPosition(_canvasRectRoot, transform.localPosition, Vector3.zero);
            }
            
            #endregion
        }
        
        
        private void Update()
        {
            if (_contextMenuTarget == null) return;
            
            var menuPointerStay = IsContextMenuPointerStay();
            if (gameObject.activeSelf && !(menuPointerStay || _contextMenuTarget.PointerStay))
            {
                Hide();
            }
            
            #region Internal
            
            bool IsContextMenuPointerStay()
            {
                for (var i = 0; i < _contextMenuViewItems.Count; i++)
                {
                    var context = _contextMenuViewItems[i];
                    if (context.PointerStay)
                    {
                        return true;
                    }
                }
                
                return false;
            }
            
            #endregion
        }
        
        private void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}