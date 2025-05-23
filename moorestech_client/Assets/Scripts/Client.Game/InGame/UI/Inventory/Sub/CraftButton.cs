﻿using System;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Tooltip;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    public class CraftButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform rectTransform;
        
        [SerializeField] private Image buttonImage;
        [SerializeField] private Color interactableColor = Color.white;
        [SerializeField] private Color nonInteractableColor = Color.gray;
        private ProgressArrowView _progressArrow;
        
        public IObservable<Unit> OnCraftFinish => _onCraftFinishSubject;
        private readonly Subject<Unit> _onCraftFinishSubject = new();
        
        private float _currentCraftTime;
        private float _buttonDownElapsed;
        private bool _isButtonDown;
        private bool _isCursorStay = true;
        private bool _isInteractable = true;
        
        private void Update()
        {
            if (_isButtonDown && _isCursorStay) _buttonDownElapsed += Time.deltaTime;
            
            if (_buttonDownElapsed >= _currentCraftTime)
            {
                _buttonDownElapsed = 0;
                _onCraftFinishSubject.OnNext(Unit.Default);
            }
            
            SetProgressAllow();
            
            
            #region Internal
            
            void SetProgressAllow()
            {
                if (!_progressArrow) return;
                
                if (_isButtonDown)
                {
                    var percent = Mathf.Clamp(_buttonDownElapsed, 0, _currentCraftTime) / _currentCraftTime;
                    _progressArrow.SetProgress(percent);
                }
                else
                {
                    _progressArrow.SetProgress(1);
                }
            }
            
  #endregion
        }
        
        public void SetCraftInfo(float craftTime, ProgressArrowView progressArrow)
        {
            _currentCraftTime = craftTime;
            _progressArrow = progressArrow;
        }
        
        private void OnDestroy()
        {
            _onCraftFinishSubject.Dispose();
        }
        
        public void SetInteractable(bool interactable)
        {
            _isInteractable = interactable;
            buttonImage.color = interactable ? interactableColor : nonInteractableColor;
            
            if (!_isInteractable)
            {
                ResetButton();
            }
        }
        
        #region このフラグはあとで決定して消す
        
        [SerializeField] private bool resetElapsedTimeOnPointerExit;
        [SerializeField] private bool stopElapsedTimeUpdateOnPointerExit;
        [SerializeField] private bool restartElapsedTimeUpdateOnPointerEnter;
        
        #endregion
        
        
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isInteractable)
            {
                _isButtonDown = true;
            }
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isInteractable)
            {
                _isButtonDown = false;
                _buttonDownElapsed = 0;
            }
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isInteractable)
            {
                MouseCursorTooltip.Instance.Show("アイテムが足りないためクラフトできません", isLocalize: false);
            }
            
            if (restartElapsedTimeUpdateOnPointerEnter) _isCursorStay = true;
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            MouseCursorTooltip.Instance.Hide();
            if (resetElapsedTimeOnPointerExit) _buttonDownElapsed = 0;
            if (stopElapsedTimeUpdateOnPointerExit) _isCursorStay = false;
        }
        
        private void OnDisable()
        {
            ResetButton();
        }
        
        private void ResetButton()
        {
            MouseCursorTooltip.Instance.Hide();
            _buttonDownElapsed = 0;
            _isCursorStay = false;
            _isButtonDown = false;
        }
    }
}