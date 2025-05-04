using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.UI.ContextMenu
{
    public class UGuiContextMenuTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerClickHandler
    {
        private bool _pointerStay;
        
        private List<ContextMenuBarInfo> _contextMenuBars;
        
        public void SetContextMenuBars(List<ContextMenuBarInfo> contextMenuBars)
        {
            _contextMenuBars = contextMenuBars;
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_pointerStay) return;
            // 左クリックだったらコンテキストメニューを開く
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                ContextMenuView.Instance.Show(_contextMenuBars);
            }
        }
        
        
        /// <summary>
        ///     フラグが変更されたあと非表示設定を行う
        /// </summary>
        private void UpdateMouseCursorTooltip()
        {
            if (!_pointerStay)
            {
                ContextMenuView.Instance.Hide();
            }
        }
        
        
        #region flagController
        
        public void OnPointerMove(PointerEventData eventData)
        {
            _pointerStay = true;
            UpdateMouseCursorTooltip();
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerStay = true;
            UpdateMouseCursorTooltip();
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerStay = false;
            UpdateMouseCursorTooltip();
        }
        
        private void OnDestroy()
        {
            _pointerStay = false;
            UpdateMouseCursorTooltip();
        }
        
        private void OnDisable()
        {
            _pointerStay = false;
            UpdateMouseCursorTooltip();
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