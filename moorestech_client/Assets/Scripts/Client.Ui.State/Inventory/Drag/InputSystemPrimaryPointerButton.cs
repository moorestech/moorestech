using System;
using Client.Input.Pointer;
using UniRx;

namespace Client.Ui.State.Inventory.Drag
{
    /// <summary>
    /// 本番の左ボタン。解放とリセットは InputSystem の流れから、押下はマウスの実状態から読む
    /// Production left button; release and reset come from InputSystem streams, pressed state from the actual mouse state
    /// </summary>
    public sealed class InputSystemPrimaryPointerButton : IPrimaryPointerButton
    {
        private readonly InputSystemPointerButtonStream _stream;

        public IObservable<Unit> OnReleased => _stream.OnPrimaryButtonReleased;
        public IObservable<Unit> OnReset => _stream.OnPrimaryButtonReset;

        public InputSystemPrimaryPointerButton(InputSystemPointerButtonStream stream)
        {
            _stream = stream;
        }

        public bool IsPressed()
        {
            return _stream.IsPrimaryButtonPressed();
        }
    }
}
