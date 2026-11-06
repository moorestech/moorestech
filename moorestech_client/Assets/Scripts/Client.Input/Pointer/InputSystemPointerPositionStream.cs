using System;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Input.Pointer
{
    /// <summary>
    /// マウス位置が変わった時だけ流す
    /// Publishes the mouse position only when it changes
    /// </summary>
    public sealed class InputSystemPointerPositionStream
    {
        private readonly Subject<Vector2> _onPointerScreenPositionChanged = new();
        private Vector2? _lastPosition;

        public IObservable<Vector2> OnPointerScreenPositionChanged => _onPointerScreenPositionChanged;

        public InputSystemPointerPositionStream()
        {
            // マウス無しなら理由を警告
            // Warn why when there is no mouse
            if (Mouse.current == null) Debug.LogWarning("[InputSystemPointerPositionStream] Mouse.current is null; no pointer position will be published until a mouse sends events");
            InputSystem.onEvent += OnInputEvent;
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            // 位置だけ読む。ボタンのみは流さない
            // Read position only; button-only changes are not published
            if (device != Mouse.current) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            if (!Mouse.current.position.ReadValueFromEvent(eventPtr, out var position)) return;
            if (_lastPosition == position) return;
            _lastPosition = position;
            _onPointerScreenPositionChanged.OnNext(position);
        }
    }
}
