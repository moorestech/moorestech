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
        [SerializeField] private float buttonDownElapsed;
        [SerializeField] private bool isButtonDown;
        private readonly Subject<Unit> _onButtonDownSubject = new();
        public IObservable<Unit> OnButtonDown => _onButtonDownSubject;

        private void Awake()
        {
            button
                .OnPointerDownAsObservable()
                .Select(_ => true)
                .Merge(
                    button.OnPointerUpAsObservable()
                        .Select(_ => false)
                )
                .Throttle(TimeSpan.FromSeconds(duration))
                .Where(x => x)
                .AsUnitObservable()
                .Subscribe(_ => _onButtonDownSubject.OnNext(Unit.Default))
                .AddTo(this);

            button.OnPointerDownAsObservable().Subscribe(_ => isButtonDown = true).AddTo(this);
            button.OnPointerUpAsObservable().Subscribe(_ =>
            {
                isButtonDown = false;
                buttonDownElapsed = 0;
            }).AddTo(this);

            Observable.EveryUpdate().Where(_ => isButtonDown).Subscribe(
                _ => buttonDownElapsed += Time.deltaTime
            ).AddTo(this);

            Observable
                .EveryUpdate()
                .Select(_ => Mathf.Clamp(buttonDownElapsed, 0, duration))
                .Select(x => x / duration)
                .Subscribe(UpdateMaskFill)
                .AddTo(this);
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
