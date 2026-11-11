using System;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Input.ArrowKeys
{
    /// <summary>
    /// キーボードの ← / → が押された瞬間に 1 回流す（UI のフォーカスに依らない。Web の window keydown の代わり）
    /// Publishes once at the moment ← or → is pressed, independent of UI focus (stands in for Web window keydown)
    /// </summary>
    public sealed class InputSystemArrowKeyStream : IHorizontalArrowKeySource
    {
        private readonly Subject<HorizontalArrowKey> _onArrowPressed = new();
        private bool _leftWasPressed;
        private bool _rightWasPressed;

        public IObservable<HorizontalArrowKey> OnArrowPressed => _onArrowPressed;

        public InputSystemArrowKeyStream()
        {
            // キーボード無しなら理由を警告
            // Warn why when there is no keyboard
            if (Keyboard.current == null) Debug.LogWarning("[InputSystemArrowKeyStream] Keyboard.current is null; no arrow press will be published until a keyboard sends events");
            InputSystem.onEvent += OnInputEvent;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            // 状態イベントから左右の矢印の押下の縁だけを流す
            // Publish only the press edges of the left and right arrows from state events
            if (device != Keyboard.current) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            PublishEdge(Keyboard.current.leftArrowKey, HorizontalArrowKey.Left, ref _leftWasPressed);
            PublishEdge(Keyboard.current.rightArrowKey, HorizontalArrowKey.Right, ref _rightWasPressed);

            #region Internal

            void PublishEdge(UnityEngine.InputSystem.Controls.KeyControl key, HorizontalArrowKey arrow, ref bool wasPressed)
            {
                // silent-ok: この状態イベントにキーの値が無い（部分の DeltaStateEvent）なら前の押下を保つ / 押下が流れないなら L0 と録画（R-12）のベルトの段で気づく
                if (!key.ReadValueFromEvent(eventPtr, out var value)) return;
                var pressed = 0.5f <= value;
                var edge = pressed && !wasPressed;
                wasPressed = pressed;
                if (edge) _onArrowPressed.OnNext(arrow);
            }

            #endregion
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            // フォーカス喪失等でキーの状態が消えたら押下の記憶を下ろす（前例 InputSystemPointerButtonStream）
            // Forget the presses when the key state is dropped on focus loss etc. (precedent InputSystemPointerButtonStream)
            if (device is not Keyboard) return;
            if (change is not (InputDeviceChange.SoftReset or InputDeviceChange.HardReset or InputDeviceChange.Removed or InputDeviceChange.Disabled)) return;
            _leftWasPressed = false;
            _rightWasPressed = false;
        }
    }
}
