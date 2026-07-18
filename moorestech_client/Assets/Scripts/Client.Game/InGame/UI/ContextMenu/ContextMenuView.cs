using System;
using System.Collections.Generic;
using Client.Game.InGame.Control;
using UniRx;
using UnityEngine;
using Client.Game.InGame.UI.UIState;

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
        private List<ContextMenuBarInfo> _contextMenuBars = new();
        private readonly Subject<Unit> _onPresentationChanged = new();

        public IObservable<Unit> OnPresentationChanged => _onPresentationChanged;
        public bool IsVisible() => gameObject.activeSelf;
        public IReadOnlyList<ContextMenuBarInfo> GetContextMenuBars() => _contextMenuBars;
        
        private void Awake()
        {
            Instance = this;
        }
        
        public void Show(UGuiContextMenuTarget currentTarget, List<ContextMenuBarInfo> contextMenuBars)
        {
            _contextMenuTarget = currentTarget;
            _contextMenuBars = contextMenuBars;
            gameObject.SetActive(true);
            menuParent.gameObject.SetActive(!WebUiScreenGate.IsWebUiMode);
            
            SetContextMenu();
            SetPosition();
            _onPresentationChanged.OnNext(Unit.Default);
            
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
            if (WebUiScreenGate.IsWebUiMode) return;
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
        
        public void Hide()
        {
            gameObject.SetActive(false);
            _contextMenuBars = new List<ContextMenuBarInfo>();
            _onPresentationChanged.OnNext(Unit.Default);
        }

        public bool TrySelect(string id)
        {
            if (!int.TryParse(id, out var index)) return false;
            if (index < 0 || index >= _contextMenuBars.Count) return false;
            _contextMenuBars[index].OnClick.Invoke();
            Hide();
            return true;
        }
    }
}
