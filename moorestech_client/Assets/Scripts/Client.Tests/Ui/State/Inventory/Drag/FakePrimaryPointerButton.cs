using System;
using Client.Ui.State.Inventory.Drag;
using UniRx;

namespace Client.Tests.Ui.State.Inventory.Drag
{
    /// <summary>
    /// 左ボタンの押下の状態と解放をテストが決める
    /// Lets a test decide the left button state and its release
    /// </summary>
    public sealed class FakePrimaryPointerButton : IPrimaryPointerButton
    {
        private readonly Subject<Unit> _onReleased = new();
        private bool _pressed;

        public IObservable<Unit> OnReleased => _onReleased;

        public FakePrimaryPointerButton(bool pressed)
        {
            _pressed = pressed;
        }

        public void SetPressed(bool pressed)
        {
            _pressed = pressed;
        }

        // 離した状態にしてから解放を流す（本番の流れと同じ順）
        // Marks the button up, then emits the release (same order as production)
        public void Release()
        {
            _pressed = false;
            _onReleased.OnNext(Unit.Default);
        }

        public bool IsPressed()
        {
            return _pressed;
        }
    }
}
