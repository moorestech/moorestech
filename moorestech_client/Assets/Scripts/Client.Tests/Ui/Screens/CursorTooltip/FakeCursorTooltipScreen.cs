using System;
using Client.Ui.Foundation.Input.Pointer;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Screens.CursorTooltip;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    // 偽の実画面とポインタと倍率。中央・倍率・ポインタ（実画面 px）をテストが決め、パネル座標は倍率で割って返す
    // Fake screen, pointer and scale; the test decides centre, scale and pointer (screen px), and panel coordinates are screen px divided by the scale
    public sealed class FakeCursorTooltipScreen : ICursorTooltipScreen, IUiScreenPointer
    {
        private readonly Subject<Vector2> _pointerMoves = new();
        private readonly Subject<Unit> _anchorModeChanges = new();
        private readonly ReactiveProperty<float> _scale = new(1f);
        private bool _locked;
        private Vector2 _lockedCenterOnScreen;
        private Vector2 _currentPointer;

        public FakeCursorTooltipScreen()
        {
            StageScale = new StageScaleSource(_scale);
        }

        public StageScaleSource StageScale { get; }

        public void SetLocked(Vector2 centerOnScreen) { _locked = true; _lockedCenterOnScreen = centerOnScreen; _anchorModeChanges.OnNext(Unit.Default); }
        public void Unlock() { _locked = false; _anchorModeChanges.OnNext(Unit.Default); }
        public void SetScale(float scale) => _scale.Value = scale;
        public void SetCurrentPointer(Vector2 screenPosition) => _currentPointer = screenPosition;
        public void MovePointer(Vector2 screenPosition) => _pointerMoves.OnNext(screenPosition);
        public bool HasPointerSubscribers => _pointerMoves.HasObservers;
        public bool HasAnchorModeSubscribers => _anchorModeChanges.HasObservers;

        public Vector2 AnchorOnScreen(Vector2 latestPointerOnScreen) => _locked ? _lockedCenterOnScreen : latestPointerOnScreen;
        public IObservable<Unit> OnAnchorModeChanged => _anchorModeChanges;
        public Vector2 CurrentPointerOnScreen() => _currentPointer;
        public IObservable<Vector2> OnPointerMovedOnScreen => _pointerMoves;
        public Vector2 ScreenToPanel(IPanel panel, Vector2 bottomLeftScreenPosition) => bottomLeftScreenPosition / _scale.Value;
    }
}
