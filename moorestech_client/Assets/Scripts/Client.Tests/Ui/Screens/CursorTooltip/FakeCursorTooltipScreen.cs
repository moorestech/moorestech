using System;
using Client.Ui.Screens.CursorTooltip;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    // 偽の実画面。ロック中の中央・倍率・今のポインタ・ポインタの移動をテストが決める（既定はロックなし・1 倍・原点）
    // Fake screen; the test decides the locked centre, scale, current pointer and pointer moves (defaults: unlocked, 1x, origin)
    public sealed class FakeCursorTooltipScreen : ICursorTooltipScreen
    {
        private readonly Subject<Vector2> _pointerMoves = new();
        private bool _locked;
        private Vector2 _lockedCenterInPanel;
        private float _scale = 1f;
        private Vector2 _currentPointer;

        public void SetLocked(Vector2 centerInPanel) { _locked = true; _lockedCenterInPanel = centerInPanel; }
        public void SetScale(float scale) => _scale = scale;
        public void SetCurrentPointer(Vector2 panelPosition) => _currentPointer = panelPosition;
        public void MovePointer(Vector2 panelPosition) => _pointerMoves.OnNext(panelPosition);
        public bool HasPointerSubscribers => _pointerMoves.HasObservers;

        public Vector2 ResolveAnchorInPanel(IPanel panel, Vector2 latestPointerInPanel) => _locked ? _lockedCenterInPanel : latestPointerInPanel;
        public Vector2 CurrentPointerInPanel(IPanel panel) => _currentPointer;
        public IObservable<Vector2> OnPointerMovedInPanel(IPanel panel) => _pointerMoves;
        public float ScreenPixelsPerPanelPixel(IPanel panel) => _scale;
    }
}
