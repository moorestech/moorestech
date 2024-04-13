using System;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.UI.Inventory.Sub
{
    public class CraftButton : MonoBehaviour
    {
        private const float TmpDuration = 5; //TODO クラフト時間を取得するようにする
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Button button;
        [SerializeField] private RectMask2D mask;
        private readonly Subject<Unit> _onCraftFinishSubject = new();

        private float _buttonDownElapsed;
        private bool _isButtonDown;
        private bool _isCursorStay = true;

        public IObservable<Unit> OnCraftFinish => _onCraftFinishSubject;

        private void Awake()
        {
            button.OnPointerDownAsObservable().Subscribe(_ =>
            {
                if (button.interactable)
                    _isButtonDown = true;
            }).AddTo(this);
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

            var percent = Mathf.Clamp(_buttonDownElapsed, 0, TmpDuration) / TmpDuration;
            UpdateMaskFill(percent);

            #region Internal

            void UpdateMaskFill(float percent)
            {
                var maxWidth = rectTransform.rect.width;
                var p = maxWidth * (1f - percent);
                mask.padding = new Vector4(0, 0, p, 0);
            }

            #endregion
        }

        private void OnDestroy()
        {
            _onCraftFinishSubject.Dispose();
        }

        public void UpdateInteractable(bool interactable)
        {
            button.interactable = interactable;
        }

        #region このフラグはあとで決定して消す

        [SerializeField] private bool resetElapsedTimeOnPointerExit;
        [SerializeField] private bool stopElapsedTimeUpdateOnPointerExit;
        [SerializeField] private bool restartElapsedTimeUpdateOnPointerEnter;

        #endregion
    }
}