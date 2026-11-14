using System;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Input.Pointer
{
    /// <summary>
    /// マウスのホイールが回った時だけ縦のノッチ数（上が正）と、そのイベントの時点のマウス位置（実画面 px・左下原点）を流す
    /// Publishes the vertical notch count (up is positive) and the mouse position at that event (screen px, bottom-left) only when the wheel turns
    /// </summary>
    public sealed class InputSystemPointerWheelStream
    {
        private readonly Subject<(float notchesUp, Vector2 screenPosition)> _onWheelNotchesScrolled = new();

        public IObservable<(float notchesUp, Vector2 screenPosition)> OnWheelNotchesScrolled => _onWheelNotchesScrolled;

        public InputSystemPointerWheelStream()
        {
            // マウス無しなら理由を警告
            // Warn why when there is no mouse
            if (Mouse.current == null) Debug.LogWarning("[InputSystemPointerWheelStream] Mouse.current is null; no wheel notch will be published until a mouse sends events");
            InputSystem.onEvent += OnInputEvent;
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            // ホイールだけ読む。入力設定が Uniform なので 1 ノッチ＝1（Windows の 120 もここで 1 に直る）
            // Read the wheel only; the input settings are Uniform, so one notch is 1 (Windows' 120 is normalized too)
            if (device != Mouse.current) return;
            // Editor の更新で届く入力は Game View の外（Inspector 等）の操作なので流さない
            // Input processed in an Editor update belongs to windows outside the Game View, so it is not published
            // silent-ok: Editor の窓の上の入力はゲームの操作でない / InputSystemPointerWheelStreamTest と EquipmentWheelInputFlowTest が見る
            if (InputState.currentUpdateType == InputUpdateType.Editor) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            if (!Mouse.current.scroll.ReadValueFromEvent(eventPtr, out var scroll)) return;
            if (scroll.y == 0f) return;
            _onWheelNotchesScrolled.OnNext((scroll.y, PositionAtEvent(eventPtr)));
        }

        private static Vector2 PositionAtEvent(InputEventPtr eventPtr)
        {
            // onEvent はイベントをデバイスへ書き込む前に呼ばれるので、デバイスの今の値はこのイベントの移動を含まない。位置はイベントから読む
            // onEvent runs before the event is written to the device, so the device value lacks this event's move; read the position from the event
            if (Mouse.current.position.ReadValueFromEvent(eventPtr, out var position)) return position;
            // 位置を含まないのはスクロールだけの DeltaStateEvent。このイベントは位置を動かさず、先のイベントは書き込み済みなので、デバイスの今の値がこのイベントの後の位置と同じ
            // Only a scroll-only DeltaStateEvent lacks position; it does not move the pointer and earlier events are already written, so the device value equals the position after this event
            return Mouse.current.position.ReadValue();
        }
    }
}
