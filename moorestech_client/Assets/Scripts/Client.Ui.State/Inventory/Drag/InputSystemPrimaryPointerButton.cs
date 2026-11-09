using System;
using Client.Input.Pointer;
using UniRx;

namespace Client.Ui.State.Inventory.Drag
{
    /// <summary>
    /// 本番の左ボタン。解放と押下の状態を同じ InputSystem のイベントの流れから読む
    /// Production left button; release and pressed state come from the same InputSystem event stream
    /// </summary>
    public sealed class InputSystemPrimaryPointerButton : IPrimaryPointerButton
    {
        private readonly InputSystemPointerButtonStream _stream;

        public IObservable<Unit> OnReleased => _stream.OnPrimaryButtonReleased;

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
