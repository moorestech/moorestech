using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Input
{
    /// <summary>
    ///     InputSystemが使えるならInputSystemのみを読み、不在時だけlegacy Inputへフォールバックする入力読み取り
    ///     Input reader that reads only the Input System when available, falling back to legacy Input only when absent
    ///
    ///     QueueStateEvent注入（プレイテスト）と実機の物理入力を同一経路で扱うための移行層。
    ///     legacy UnityEngine.Input直読みはInputSystemイベント注入で駆動できないため、ここを経由する。
    ///     両系統のOR読みは禁止: Windows実機では同一押下がWM_INPUTとWM_KEYDOWNで別フレームに帰属し、
    ///     Down判定が2回発火してトグルUIが開いた直後に閉じる実害があった（2026-07-31）。
    ///     Migration layer letting QueueStateEvent injection (playtests) and real hardware share one path.
    ///     Direct legacy UnityEngine.Input reads cannot be driven by Input System event injection, so go through here.
    ///     Never OR-read both backends: on Windows one physical press lands on different frames via WM_INPUT vs
    ///     WM_KEYDOWN, firing Down twice and instantly closing toggle UIs (observed 2026-07-31).
    /// </summary>
    public static class HybridInput
    {
        public static Vector3 GetMousePosition()
        {
            // InputSystemのマウス座標を使う（実機と入力注入の双方を同一経路で扱う）
            // Use the Input System mouse position so real and injected input share one path
            return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : UnityEngine.Input.mousePosition;
        }

        // このAPIが返す移動量の単位はピクセル。カーソルロック中は座標が凍結するため移動量はdeltaで読む
        // This API returns movement in pixels; the cursor lock freezes the position, so movement must be read as a delta
        //
        // legacy軸値はピクセルでなく正規化値のためフォールバックしない。Mouse不在時は移動量そのものが未計測
        // No legacy fallback: GetAxis returns normalized values, not pixels, so without a Mouse there is simply no measurement
        //
        // 未計測をzeroで表すと「動いていない」と同義になるため、計測できたかを戻り値で返す
        // Reporting zero for an unmeasured frame would mean "did not move", so measurement success is returned separately
        public static bool TryGetMouseDelta(out Vector2 deltaPixels)
        {
            if (Mouse.current == null)
            {
                deltaPixels = Vector2.zero;
                return false;
            }

            deltaPixels = Mouse.current.delta.ReadValue();
            return true;
        }

        public static bool GetKeyDown(KeyCode keyCode)
        {
            var key = ToInputSystemKey(keyCode);
            var pressed = key.HasValue && Keyboard.current != null
                ? Keyboard.current[key.Value].wasPressedThisFrame
                : UnityEngine.Input.GetKeyDown(keyCode);
            return Suppress(pressed, InputSuppressionScope.Keyboard);
        }

        public static bool GetKey(KeyCode keyCode)
        {
            var key = ToInputSystemKey(keyCode);
            var held = key.HasValue && Keyboard.current != null
                ? Keyboard.current[key.Value].isPressed
                : UnityEngine.Input.GetKey(keyCode);
            return Suppress(held, InputSuppressionScope.Keyboard);
        }

        // 解放通知を抑止するとホールド系修飾キーが押しっぱなしで固着するためGetKeyUpは抑止を通さない
        // GetKeyUp skips suppression: a suppressed release would leave hold-style modifiers stuck down
        public static bool GetKeyUp(KeyCode keyCode)
        {
            var key = ToInputSystemKey(keyCode);
            return key.HasValue && Keyboard.current != null
                ? Keyboard.current[key.Value].wasReleasedThisFrame
                : UnityEngine.Input.GetKeyUp(keyCode);
        }

        public static bool GetMouseButtonDown(int button)
        {
            var control = GetMouseButtonControl(button);
            return control != null ? control.wasPressedThisFrame : UnityEngine.Input.GetMouseButtonDown(button);
        }

        public static bool GetMouseButtonUp(int button)
        {
            var control = GetMouseButtonControl(button);
            return control != null ? control.wasReleasedThisFrame : UnityEngine.Input.GetMouseButtonUp(button);
        }

        public static bool GetMouseButton(int button)
        {
            var control = GetMouseButtonControl(button);
            return control != null ? control.isPressed : UnityEngine.Input.GetMouseButton(button);
        }

        private static bool Suppress(bool value, InputSuppressionScope scope)
        {
            if (!value || !WebUiInputExclusivity.IsSuppressed(scope)) return value;
            WebUiInputExclusivity.ProbeSuppressed(scope);
            return false;
        }

        private static UnityEngine.InputSystem.Controls.ButtonControl GetMouseButtonControl(int button)
        {
            var mouse = Mouse.current;
            if (mouse == null) return null;

            return button switch
            {
                0 => mouse.leftButton,
                1 => mouse.rightButton,
                2 => mouse.middleButton,
                _ => null,
            };
        }

        private static Key? ToInputSystemKey(KeyCode keyCode)
        {
            // 使用箇所があるキーのみ対応。未対応キーはlegacy読みのみとなる
            // Covers only the keys actually used; unmapped keys fall back to legacy-only reads
            return keyCode switch
            {
                KeyCode.B => Key.B,
                KeyCode.A => Key.A,
                KeyCode.D => Key.D,
                KeyCode.E => Key.E,
                KeyCode.F1 => Key.F1,
                KeyCode.F2 => Key.F2,
                KeyCode.I => Key.I,
                KeyCode.Q => Key.Q,
                KeyCode.R => Key.R,
                KeyCode.T => Key.T,
                KeyCode.U => Key.U,
                KeyCode.V => Key.V,
                KeyCode.W => Key.W,
                KeyCode.S => Key.S,
                KeyCode.Tab => Key.Tab,
                KeyCode.F3 => Key.F3,
                KeyCode.LeftShift => Key.LeftShift,
                KeyCode.LeftControl => Key.LeftCtrl,
                KeyCode.LeftCommand => Key.LeftCommand,
                KeyCode.LeftAlt => Key.LeftAlt,
                KeyCode.Z => Key.Z,
                _ => null,
            };
        }
    }
}
