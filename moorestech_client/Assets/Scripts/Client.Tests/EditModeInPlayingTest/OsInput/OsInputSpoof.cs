using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Tests.EditModeInPlayingTest.OsInput
{
    /// <summary>
    /// OS レベルでキー・マウス入力を合成イベントとして注入するユーティリティ
    /// Utility for injecting synthetic key/mouse events at the OS level.
    ///
    /// Editor (macOS/Windows): KeyboardState / MouseState + QueueStateEvent 経由で直接注入（フォーカス不要）
    /// Editor (macOS/Windows): inject via KeyboardState / MouseState + QueueStateEvent (no focus needed)
    ///
    /// macOS Standalone: CGEvent kCGHIDEventTap 経由（要 Accessibility 許可）
    /// macOS Standalone: via CGEvent kCGHIDEventTap (Accessibility permission required)
    ///
    /// Windows Standalone: user32.dll の SendInput を使用（UIPI 制約に注意）
    /// Windows Standalone: via user32.dll SendInput (note UIPI constraints)
    /// </summary>
    public static class OsInputSpoof
    {
        /// <summary>
        /// OS 入力の合成に使用するキー定義（US 配列想定）
        /// Key definitions for OS input synthesis (US keyboard layout assumed)
        /// </summary>
        public enum DebugKey
        {
            W, A, S, D,
            Space, Enter, Escape, Tab,
            LeftShift, LeftCtrl,
            Up, Down, Left, Right,
            E,
        }

        /// <summary>
        /// 現在の環境で OS 入力注入が使用可能かどうか
        /// Whether OS input injection is available in the current environment.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
#if UNITY_EDITOR
                // Editor では InputSystem.QueueStateEvent を使用するため常に true
                // Editor uses InputSystem.QueueStateEvent, so always available
                return true;
#elif UNITY_STANDALONE_WIN
                return true;
#elif UNITY_STANDALONE_OSX
                return MacIsAccessibilityTrusted();
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Unity Editor を OS レベルで最前面に持ってくる
        /// Bring Unity Editor to the front at OS level.
        /// </summary>
        public static void ActivateApp()
        {
#if UNITY_STANDALONE_OSX
            OsInputMac_ActivateApp();
#endif
            // macOS Editor / Windows では不要
            // No-op on macOS Editor and Windows
        }

        public static void KeyDown(DebugKey key)
        {
#if UNITY_EDITOR
            EditorInjectKey(key, true);
#elif UNITY_STANDALONE_OSX
            OsInputMac_Key(MacKeyCode(key), true);
#elif UNITY_STANDALONE_WIN
            WinSendKey(key, isDown: true);
#endif
        }

        public static void KeyUp(DebugKey key)
        {
#if UNITY_EDITOR
            EditorInjectKey(key, false);
#elif UNITY_STANDALONE_OSX
            OsInputMac_Key(MacKeyCode(key), false);
#elif UNITY_STANDALONE_WIN
            WinSendKey(key, isDown: false);
#endif
        }

        public static void MouseMoveBy(int dx, int dy)
        {
#if UNITY_EDITOR
            EditorInjectMouseMove(dx, dy);
#elif UNITY_STANDALONE_OSX
            OsInputMac_MouseMoveBy(dx, dy);
#elif UNITY_STANDALONE_WIN
            WinMouseMove(dx, dy);
#endif
        }

        public static void MouseLeftClick()
        {
#if UNITY_EDITOR
            EditorInjectMouseClick();
#elif UNITY_STANDALONE_OSX
            OsInputMac_MouseLeftClick();
#elif UNITY_STANDALONE_WIN
            WinMouseClick();
#endif
        }

#if UNITY_EDITOR
        // ----------------------------------------------------------------
        // Editor (macOS/Windows): KeyboardState / MouseState + QueueStateEvent 経由で注入
        // Editor (macOS/Windows): inject via KeyboardState / MouseState + QueueStateEvent
        //
        // StateEvent.From は Windows Editor で状態が反映されない問題があるため、
        // 型付き状態構造体（KeyboardState / MouseState）を使用する。
        // StateEvent.From has issues on Windows Editor where state is not applied,
        // so we use typed state structs (KeyboardState / MouseState) instead.
        // ----------------------------------------------------------------

        private static readonly System.Collections.Generic.HashSet<Key> EditorPressedKeys = new();

        private static void EditorInjectKey(DebugKey key, bool isDown)
        {
            // キーボードデバイスが存在しない場合は何もしない
            // Do nothing if keyboard device is absent
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 押下キーセットを更新して KeyboardState を送信
            // Update pressed key set and send KeyboardState
            var inputKey = DebugKeyToKey(key);
            if (isDown) EditorPressedKeys.Add(inputKey);
            else EditorPressedKeys.Remove(inputKey);

            var keysArray = new Key[EditorPressedKeys.Count];
            EditorPressedKeys.CopyTo(keysArray);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keysArray));
        }

        private static void EditorInjectMouseMove(int dx, int dy)
        {
            // マウスデバイスが存在しない場合は何もしない
            // Do nothing if mouse device is absent
            var mouse = Mouse.current;
            if (mouse == null) return;

            // 現在位置にデルタを加算した MouseState を送信
            // Send MouseState with delta added to current position
            var currentPos = mouse.position.ReadValue();
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = currentPos + new Vector2(dx, dy),
                delta = new Vector2(dx, dy),
            });
        }

        private static void EditorInjectMouseClick()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var currentPos = mouse.position.ReadValue();

            // ボタン押下 → 解放の順で注入（buttons bit0 = 左ボタン）
            // Inject button down then up (buttons bit0 = left button)
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = currentPos,
                buttons = 1,
            });
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = currentPos,
                buttons = 0,
            });
        }

        /// <summary>
        /// DebugKey → InputSystem.Key 変換
        /// DebugKey to InputSystem.Key conversion
        /// </summary>
        private static Key DebugKeyToKey(DebugKey k) => k switch
        {
            DebugKey.W         => Key.W,
            DebugKey.A         => Key.A,
            DebugKey.S         => Key.S,
            DebugKey.D         => Key.D,
            DebugKey.E         => Key.E,
            DebugKey.Space     => Key.Space,
            DebugKey.Enter     => Key.Enter,
            DebugKey.Escape    => Key.Escape,
            DebugKey.Tab       => Key.Tab,
            DebugKey.LeftShift => Key.LeftShift,
            DebugKey.LeftCtrl  => Key.LeftCtrl,
            DebugKey.Up        => Key.UpArrow,
            DebugKey.Down      => Key.DownArrow,
            DebugKey.Left      => Key.LeftArrow,
            DebugKey.Right     => Key.RightArrow,
            _                  => throw new ArgumentOutOfRangeException(nameof(k), k, null),
        };
#endif

#if UNITY_STANDALONE_OSX
        // ----------------------------------------------------------------
        // macOS Standalone: OsInputMac ネイティブプラグイン経由（CGEvent）
        // macOS Standalone: via OsInputMac native plugin (CGEvent)
        // ----------------------------------------------------------------

        [DllImport("OsInputMac")]
        private static extern void OsInputMac_ActivateApp();

        [DllImport("OsInputMac")]
        private static extern bool OsInputMac_IsAccessibilityTrusted();

        [DllImport("OsInputMac")]
        private static extern void OsInputMac_RequestAccessibility();

        [DllImport("OsInputMac")]
        private static extern void OsInputMac_Key(ushort keyCode, bool isDown);

        [DllImport("OsInputMac")]
        private static extern void OsInputMac_MouseMoveBy(double dx, double dy);

        [DllImport("OsInputMac")]
        private static extern void OsInputMac_MouseLeftClick();

        public static bool MacIsAccessibilityTrusted() => OsInputMac_IsAccessibilityTrusted();
        public static void RequestMacAccessibility()   => OsInputMac_RequestAccessibility();

        // CGKeyCode マッピング（US ANSI キーボード想定）
        // CGKeyCode mapping for US ANSI keyboard
        private static ushort MacKeyCode(DebugKey k) => k switch
        {
            DebugKey.A         => 0,
            DebugKey.S         => 1,
            DebugKey.D         => 2,
            DebugKey.W         => 13,
            DebugKey.E         => 14,
            DebugKey.Tab       => 48,
            DebugKey.Space     => 49,
            DebugKey.Enter     => 36,
            DebugKey.Escape    => 53,
            DebugKey.LeftShift => 56,
            DebugKey.LeftCtrl  => 59,
            DebugKey.Left      => 123,
            DebugKey.Right     => 124,
            DebugKey.Down      => 125,
            DebugKey.Up        => 126,
            _ => throw new ArgumentOutOfRangeException(nameof(k), k, null),
        };
#endif

#if UNITY_STANDALONE_WIN
        // ----------------------------------------------------------------
        // Windows Standalone: user32.dll SendInput 経由
        // Windows Standalone: via user32.dll SendInput
        // ----------------------------------------------------------------

        private const int  INPUT_KEYBOARD       = 1;
        private const int  INPUT_MOUSE          = 0;
        private const uint KEYEVENTF_SCANCODE   = 0x0008;
        private const uint KEYEVENTF_KEYUP      = 0x0002;
        private const uint MOUSEEVENTF_MOVE     = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP   = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int    type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT  mi;
            [FieldOffset(0)] public KEYBDINPUT  ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int    dx;
            public int    dy;
            public uint   mouseData;
            public uint   dwFlags;
            public uint   time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint   dwFlags;
            public uint   time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // スキャンコードマッピング（Set 1 / US 配列）
        // Scan code mapping (Set 1 / US layout)
        private static ushort WinScanCode(DebugKey k) => k switch
        {
            DebugKey.Escape    => 0x01,
            DebugKey.Tab       => 0x0F,
            DebugKey.A         => 0x1E,
            DebugKey.S         => 0x1F,
            DebugKey.D         => 0x20,
            DebugKey.E         => 0x12,
            DebugKey.W         => 0x11,
            DebugKey.LeftCtrl  => 0x1D,
            DebugKey.Enter     => 0x1C,
            DebugKey.LeftShift => 0x2A,
            DebugKey.Space     => 0x39,
            DebugKey.Up        => 0x48,
            DebugKey.Down      => 0x50,
            DebugKey.Left      => 0x4B,
            DebugKey.Right     => 0x4D,
            _ => throw new ArgumentOutOfRangeException(nameof(k), k, null),
        };

        private static void WinSendKey(DebugKey key, bool isDown)
        {
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                U    = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk     = 0,
                        wScan   = WinScanCode(key),
                        dwFlags = KEYEVENTF_SCANCODE | (isDown ? 0u : KEYEVENTF_KEYUP),
                    },
                },
            };
            WinSendInput(new[] { input });
        }

        private static void WinMouseMove(int dx, int dy)
        {
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                U    = new InputUnion { mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = MOUSEEVENTF_MOVE } },
            };
            WinSendInput(new[] { input });
        }

        private static void WinMouseClick()
        {
            var down = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } } };
            var up   = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP   } } };
            WinSendInput(new[] { down, up });
        }

        private static void WinSendInput(INPUT[] inputs)
        {
            // UIPI 制約で失敗する場合は管理者権限の差を確認すること
            // If fails due to UIPI, check administrator privilege difference
            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent != inputs.Length)
                throw new InvalidOperationException($"SendInput failed: sent={sent}/{inputs.Length}, err={Marshal.GetLastWin32Error()}");
        }
#endif
    }
}
