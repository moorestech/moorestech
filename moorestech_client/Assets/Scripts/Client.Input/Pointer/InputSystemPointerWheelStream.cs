using System;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Input.Pointer
{
    /// <summary>
    /// マウスのホイールが回った時だけ縦のノッチ数（上が正）を流す
    /// Publishes the vertical notch count (up is positive) only when the mouse wheel turns
    /// </summary>
    public sealed class InputSystemPointerWheelStream
    {
        private readonly Subject<float> _onWheelNotchesScrolled = new();

        public IObservable<float> OnWheelNotchesScrolled => _onWheelNotchesScrolled;

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
            _onWheelNotchesScrolled.OnNext(scroll.y);
        }
    }
}
