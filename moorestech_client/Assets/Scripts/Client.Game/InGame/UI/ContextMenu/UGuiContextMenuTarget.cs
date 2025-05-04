using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.UI.ContextMenu
{
    public class UGuiContextMenuTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerClickHandler
    {
        public bool PointerStay { get; private set; }
        
        private List<ContextMenuBarInfo> _contextMenuBars;
        
        public void SetContextMenuBars(List<ContextMenuBarInfo> contextMenuBars)
        {
            _contextMenuBars = contextMenuBars;
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!PointerStay) return;
            // 左クリックだったらコンテキストメニューを開く
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                ContextMenuView.Instance.Show(this, _contextMenuBars);
            }
        }
        
        
        #region flagController
        
        public void OnPointerMove(PointerEventData eventData)
        {
            PointerStay = true;
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerStay = true;
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            PointerStay = false;
        }
        
        private void OnDestroy()
        {
            PointerStay = false;
        }
        
        private void OnDisable()
        {
            PointerStay = false;
        }
        
        #endregion
    }
    
    public class ContextMenuBarInfo
    {
        public readonly string Title;
        public readonly Action OnClick;
        
        public ContextMenuBarInfo(string title, Action onClick)
        {
            Title = title;
            OnClick = onClick;
        }
    }
}