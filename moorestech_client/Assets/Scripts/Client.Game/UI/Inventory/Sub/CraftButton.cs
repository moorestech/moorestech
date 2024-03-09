using System;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.Sub
{
    public class CraftButton : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Button button;
        [SerializeField] private RectMask2D mask;
        [SerializeField] private float duration;
        [SerializeField] private bool resetElapsedTimeOnPointerExit;
        [SerializeField] private bool stopElapsedTimeUpdateOnPointerExit;
        [SerializeField] private bool restartElapsedTimeUpdateOnPointerEnter;
        private readonly Subject<Unit> _onButtonDownSubject = new();
        private float _buttonDownElapsed;
        private bool _isButtonDown;
        public IObservable<Unit> OnButtonDown => _onButtonDownSubject;

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
                if (stopElapsedTimeUpdateOnPointerExit) _isButtonDown = false;
            });
            button.OnPointerEnterAsObservable().Subscribe(_ =>
            {
                if (restartElapsedTimeUpdateOnPointerEnter) _isButtonDown = true;
            });
        }

        private void Update()
        {
            if (_isButtonDown) _buttonDownElapsed += Time.deltaTime;
            if (_buttonDownElapsed >= duration)
            {
                _buttonDownElapsed = 0;
                _onButtonDownSubject.OnNext(Unit.Default);
            }

            UpdateMaskFill(Mathf.Clamp(_buttonDownElapsed, 0, duration) / duration);
        }

        private void OnDestroy()
        {
            _onButtonDownSubject.Dispose();
        }

        private void UpdateMaskFill(float percent)
        {
            var maxWidth = rectTransform.rect.width;
            var p = maxWidth * (1f - percent);
            mask.padding = new Vector4(0, 0, p, 0);
        }

        public void UpdateInteractable(bool interactable)
        {
            button.interactable = interactable;
        }
    }
}
