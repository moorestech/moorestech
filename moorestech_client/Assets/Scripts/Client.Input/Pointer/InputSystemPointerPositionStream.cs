using System;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Input.Pointer
{
    /// <summary>
    /// InputSystem のマウスの状態イベントから位置を読み、変わった時だけ流す（毎フレームの比較はしない）
    /// Reads the position from InputSystem mouse state events and publishes it only when it changes (no per-frame comparison)
    /// </summary>
    public sealed class InputSystemPointerPositionStream : IPointerPositionStream
    {
        private readonly Subject<Vector2> _onPointerScreenPositionChanged = new();
        private Vector2? _lastPosition;

        public IObservable<Vector2> OnPointerScreenPositionChanged => _onPointerScreenPositionChanged;

        public InputSystemPointerPositionStream()
        {
            // マウスが無いと何も流れないので、ポインタに付いて動く UI が止まる理由を出しておく
            // Nothing flows without a mouse, so report why pointer-following UI will not move
            if (Mouse.current == null) Debug.LogWarning("[InputSystemPointerPositionStream] Mouse.current is null; no pointer position will be published until a mouse sends events");
            InputSystem.onEvent += OnInputEvent;
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            // HybridInput と同じ Mouse.current の位置だけを読む。ボタンだけの変化は位置が同じなので流さない
            // Read only Mouse.current's position like HybridInput; button-only changes keep the same position and are not published
            if (device != Mouse.current) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            if (!Mouse.current.position.ReadValueFromEvent(eventPtr, out var position)) return;
            if (_lastPosition == position) return;
            _lastPosition = position;
            _onPointerScreenPositionChanged.OnNext(position);
        }
    }
}
