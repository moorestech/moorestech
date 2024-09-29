using System;
using Client.Game.InGame.UI.Util;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    public class CraftButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float TmpDuration = 5; //TODO クラフト時間を取得するようにする
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Button button;
        
        [SerializeField] private RectMask2D mask;
        [SerializeField] private float filledPadding = 13.2f;
        [SerializeField] private float unfilledPadding = 89.3f;
        
        private float _buttonDownElapsed;
        private bool _isButtonDown;
        private bool _isCursorStay = true;
        
        public IObservable<Unit> OnCraftFinish => _onCraftFinishSubject;
        private readonly Subject<Unit> _onCraftFinishSubject = new();
        
        private void Awake()
        {
            button.OnPointerDownAsObservable().Subscribe(_ => _isButtonDown = true).AddTo(this);
            button.OnPointerUpAsObservable().Subscribe(_ =>
            {
                _isButtonDown = false;
                _buttonDownElapsed = 0;
            }).AddTo(this);
            button.OnPointerExitAsObservable().Subscribe(_ =>
            {
                if (resetElapsedTimeOnPointerExit) _buttonDownElapsed = 0;
                if (stopElapsedTimeUpdateOnPointerExit) _isCursorStay = false;
            });
            button.OnPointerEnterAsObservable().Subscribe(_ =>
            {
                if (restartElapsedTimeUpdateOnPointerEnter) _isCursorStay = true;
            });
        }
        
        private void Update()
        {
            if (_isButtonDown && _isCursorStay) _buttonDownElapsed += Time.deltaTime;
            
            if (_buttonDownElapsed >= TmpDuration)
            {
                _buttonDownElapsed = 0;
                _onCraftFinishSubject.OnNext(Unit.Default);
            }
            
            if (_isButtonDown)
            {
                var percent = Mathf.Clamp(_buttonDownElapsed, 0, TmpDuration) / TmpDuration;
                UpdateMaskFill(percent);
            }
            else
            {
                mask.padding = new Vector4(unfilledPadding, 0, 0, 0);
            }
            
            
            #region Internal
            
            void UpdateMaskFill(float percent)
            {
                var p = Mathf.Lerp(filledPadding, unfilledPadding, percent);
                mask.padding = new Vector4(p, 0, 0, 0);
            }
            
            #endregion
        }
        
        private void OnDestroy()
        {
            _onCraftFinishSubject.Dispose();
        }
        
        public void SetInteractable(bool interactable)
        {
            Debug.Log("aaaa");
            button.interactable = interactable;
        }
        
        #region このフラグはあとで決定して消す
        
        [SerializeField] private bool resetElapsedTimeOnPointerExit;
        [SerializeField] private bool stopElapsedTimeUpdateOnPointerExit;
        [SerializeField] private bool restartElapsedTimeUpdateOnPointerEnter;
        
        #endregion
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            MouseCursorExplainer.Instance.Show("");
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            MouseCursorExplainer.Instance.Hide();
        }
        
        private void OnDisable()
        {
            MouseCursorExplainer.Instance.Hide();
        }
    }
}