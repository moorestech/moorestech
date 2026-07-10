using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Input
{
    /// <summary>
    ///     InputSystemを優先しlegacy Inputへフォールバックするハイブリッド入力読み取り
    ///     Hybrid input reader that prefers the Input System and falls back to legacy Input
    ///
    ///     QueueStateEvent注入（プレイテスト）と実機の物理入力を同一経路で扱うための移行層。
    ///     legacy UnityEngine.Input直読みはInputSystemイベント注入で駆動できないため、ここを経由する。
    ///     Migration layer letting QueueStateEvent injection (playtests) and real hardware share one path.
    ///     Direct legacy UnityEngine.Input reads cannot be driven by Input System event injection, so go through here.
    /// </summary>
    public static class HybridInput
    {
        public static Vector3 GetMousePosition()
        {
            // InputSystemのマウス座標を使う（実機と入力注入の双方を同一経路で扱う）
            // Use the Input System mouse position so real and injected input share one path
            return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : UnityEngine.Input.mousePosition;
        }

        public static bool GetKeyDown(KeyCode keyCode)
        {
            var key = ToInputSystemKey(keyCode);
            var inputSystemPressed = key.HasValue && Keyboard.current != null && Keyboard.current[key.Value].wasPressedThisFrame;
            return inputSystemPressed || UnityEngine.Input.GetKeyDown(keyCode);
        }

        public static bool GetKey(KeyCode keyCode)
        {
            var key = ToInputSystemKey(keyCode);
            var inputSystemHeld = key.HasValue && Keyboard.current != null && Keyboard.current[key.Value].isPressed;
            return inputSystemHeld || UnityEngine.Input.GetKey(keyCode);
        }

        public static bool GetMouseButtonDown(int button)
        {
            var control = GetMouseButtonControl(button);
            return (control != null && control.wasPressedThisFrame) || UnityEngine.Input.GetMouseButtonDown(button);
        }

        public static bool GetMouseButtonUp(int button)
        {
            var control = GetMouseButtonControl(button);
            return (control != null && control.wasReleasedThisFrame) || UnityEngine.Input.GetMouseButtonUp(button);
        }

        public static bool GetMouseButton(int button)
        {
            var control = GetMouseButtonControl(button);
            return (control != null && control.isPressed) || UnityEngine.Input.GetMouseButton(button);
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
                KeyCode.E => Key.E,
                KeyCode.Q => Key.Q,
                KeyCode.R => Key.R,
                KeyCode.T => Key.T,
                KeyCode.U => Key.U,
                KeyCode.V => Key.V,
                KeyCode.Tab => Key.Tab,
                KeyCode.F3 => Key.F3,
                KeyCode.LeftShift => Key.LeftShift,
                KeyCode.LeftControl => Key.LeftCtrl,
                _ => null,
            };
        }
    }
}
