using System;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Input.Pointer
{
    /// <summary>
    /// マウスの左ボタンが離れたフレームに 1 回流す（Web の window mouseup の代わり。UI の拾いに依らない）
    /// Publishes once in the frame the left mouse button is released (stands in for Web's window mouseup; independent of UI picking)
    /// </summary>
    public sealed class InputSystemPointerButtonStream
    {
        private readonly Subject<Unit> _onPrimaryButtonReleased = new();
        private bool _wasPressed;

        public IObservable<Unit> OnPrimaryButtonReleased => _onPrimaryButtonReleased;

        public InputSystemPointerButtonStream()
        {
            // マウス無しなら理由を警告
            // Warn why when there is no mouse
            if (Mouse.current == null) Debug.LogWarning("[InputSystemPointerButtonStream] Mouse.current is null; no button release will be published until a mouse sends events");
            InputSystem.onEvent += OnInputEvent;
        }

        // 今のマウスの左ボタンの実状態。解放を取りこぼしても（フォーカス喪失のデバイスリセット等）実状態へ戻る（D-20・D-30）
        // The mouse's actual left button state; it recovers even when a release was missed (device reset on focus loss, etc.; D-20, D-30)
        public bool IsPrimaryButtonPressed()
        {
            // silent-ok: マウスが無ければ押していない扱い / 生成時の警告（Mouse.current is null）で開発者が気づく
            if (Mouse.current == null) return false;
            return Mouse.current.leftButton.isPressed;
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            // 左ボタンの押下→解放の変わり目だけを流す
            // Publish only the pressed-to-released edge of the left button
            if (device != Mouse.current) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            if (!Mouse.current.leftButton.ReadValueFromEvent(eventPtr, out var value)) return;
            var pressed = 0.5f <= value;
            var released = _wasPressed && !pressed;
            _wasPressed = pressed;
            if (released) _onPrimaryButtonReleased.OnNext(Unit.Default);
        }
    }
}
