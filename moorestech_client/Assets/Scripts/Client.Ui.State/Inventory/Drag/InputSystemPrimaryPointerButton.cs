using System;
using Client.Input.Pointer;
using UniRx;

namespace Client.Ui.State.Inventory.Drag
{
    /// <summary>
    /// 本番の左ボタン。解放は InputSystem のイベントの流れから、押下はマウスの実状態から読む
    /// Production left button; release comes from the InputSystem event stream, pressed state from the actual mouse state
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
