using Client.Ui.Foundation.Input.Pointer;
using System;
using Client.Ui.Screens.GrabOverlay;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.GrabOverlay
{
    /// <summary>
    /// テストが動かすポインタ。画面 px ÷ 倍率をパネル座標として返す（左上原点で扱う）
    /// Test-driven pointer; returns screen px divided by the scale as panel coordinates (top-left origin)
    /// </summary>
    public sealed class FakeUiScreenPointer : IUiScreenPointer
    {
        private readonly Subject<Vector2> _onPointerMovedOnScreen = new();
        private readonly float _screenPixelsPerPanelPixel;
        private Vector2 _current;

        public IObservable<Vector2> OnPointerMovedOnScreen => _onPointerMovedOnScreen;

        public FakeUiScreenPointer(float screenPixelsPerPanelPixel)
        {
            _screenPixelsPerPanelPixel = screenPixelsPerPanelPixel;
        }

        // 位置だけ変える（イベントを流さない＝非表示の間に動いた状態を作る）
        // Changes only the position without an event (models moving while hidden)
        public void SetCurrent(Vector2 panelPosition)
        {
            _current = panelPosition * _screenPixelsPerPanelPixel;
        }

        public void Move(Vector2 panelPosition)
        {
            SetCurrent(panelPosition);
            _onPointerMovedOnScreen.OnNext(_current);
        }

        public Vector2 CurrentPointerOnScreen()
        {
            return _current;
        }

        public Vector2 ScreenToPanel(IPanel panel, Vector2 pointerOnScreen)
        {
            return pointerOnScreen / _screenPixelsPerPanelPixel;
        }
    }
}
