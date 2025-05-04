using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.ContextMenu
{
    public class ContextMenuViewItem : MonoBehaviour
    {
        public bool PointerStay => contextMenuRaycastTarget.PointerStay;
        [SerializeField] private ContextMenuRaycastTarget contextMenuRaycastTarget;
        
        public IObservable<Unit> OnPointerClick => _onPointerClick;
        private readonly Subject<Unit> _onPointerClick = new();
        
        [SerializeField] private TMP_Text barTitle;
        [SerializeField] private Button itemButton;
        
        private ContextMenuBarInfo _contextMenuBarInfo;
        private void Awake()
        {
            itemButton.onClick.AddListener( () =>
            {
                if (_contextMenuBarInfo == null) return;
                _onPointerClick.OnNext(Unit.Default);
                _contextMenuBarInfo.OnClick.Invoke();
            });
        }
        
        
        public void Initialize(ContextMenuBarInfo contextMenuBarInfo)
        {
            _contextMenuBarInfo = contextMenuBarInfo;
            barTitle.text = contextMenuBarInfo.Title;
        }
    }
}