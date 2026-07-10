using System.Collections.Generic;
using Client.Playtest.Overlay;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Playtest.Input
{
    /// <summary>
    ///     QueueStateEventによるキー・マウス注入層（プレイテスト用）
    ///     Key/mouse injection layer via QueueStateEvent (for playtests)
    ///
    ///     InputSystem.Update()は呼ばず、通常フレームのInputSystem更新に処理を委ねる。
    ///     注入後は必ず1フレーム以上待ってから状態を読むこと（TapKey/Click等のasync APIを使う）。
    ///     Never calls InputSystem.Update(); queued events are consumed by the normal per-frame update.
    ///     Always wait at least one frame after injecting before reading state (use the async APIs like TapKey/Click).
    /// </summary>
    public static class SemanticInput
    {
        // 押下中キー集合。KeyboardStateは全量スナップショットなので毎回全押下キーを詰め直す
        // Set of held keys; KeyboardState is a full snapshot, so re-pack every held key each time
        private static readonly HashSet<Key> PressedKeys = new();

        public static void EnsureDevices()
        {
            // バッチ・ヘッドレス環境でデバイスが無い場合のみ仮想デバイスを生成する
            // Create virtual devices only when absent (batch/headless environments)
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
        }

        // ---- キーボード / Keyboard ----

        public static void KeyDown(Key key)
        {
            EnsureDevices();
            PressedKeys.Add(key);
            QueueKeyboardState();
            PlaytestOverlay.NotifyKey(key, true);
        }

        public static void KeyUp(Key key)
        {
            EnsureDevices();
            PressedKeys.Remove(key);
            QueueKeyboardState();
            PlaytestOverlay.NotifyKey(key, false);
        }

        public static async UniTask TapKey(Key key)
        {
            // 押下と解放を別フレームに分離し、GetKeyDown/GetKeyUpの両方を確実に発火させる
            // Separate press and release into different frames so both GetKeyDown and GetKeyUp fire reliably
            KeyDown(key);
            await UniTask.DelayFrame(2);
            KeyUp(key);
            await UniTask.DelayFrame(2);
        }

        public static void ReleaseAllKeys()
        {
            PressedKeys.Clear();
            PlaytestOverlay.NotifyAllKeysReleased();
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        }

        // ---- マウス / Mouse ----

        public static void MouseMoveTo(Vector2 screenPosition)
        {
            EnsureDevices();
            // deltaを0にしてカメラ回転（Look入力）へ波及させず、絶対座標のみ更新する
            // Keep delta at zero so camera look input is untouched; update only the absolute position
            InputSystem.QueueStateEvent(Mouse.current, new MouseState
            {
                position = screenPosition,
                delta = Vector2.zero,
                buttons = CurrentButtons(),
            });
        }

        public static void MouseButtonDown(int button)
        {
            EnsureDevices();
            QueueMouseButtons((ushort)(CurrentButtons() | ButtonBit(button)));
            PlaytestOverlay.NotifyMouseButton(button, true);
        }

        public static void MouseButtonUp(int button)
        {
            EnsureDevices();
            QueueMouseButtons((ushort)(CurrentButtons() & ~ButtonBit(button)));
            PlaytestOverlay.NotifyMouseButton(button, false);
        }

        public static async UniTask Click()
        {
            // 左ボタンの押下→解放を別フレームで注入する（設置系はGetKeyUpで確定するため解放が必須）
            // Inject left-button press then release on separate frames (placement commits on GetKeyUp)
            MouseButtonDown(0);
            await UniTask.DelayFrame(2);
            MouseButtonUp(0);
            await UniTask.DelayFrame(2);
        }

        public static Vector2 CurrentMousePosition()
        {
            EnsureDevices();
            return Mouse.current.position.ReadValue();
        }

        #region Internal

        private static void QueueKeyboardState()
        {
            var keys = new Key[PressedKeys.Count];
            PressedKeys.CopyTo(keys);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
        }

        private static void QueueMouseButtons(ushort buttons)
        {
            InputSystem.QueueStateEvent(Mouse.current, new MouseState
            {
                position = Mouse.current.position.ReadValue(),
                delta = Vector2.zero,
                buttons = buttons,
            });
        }

        private static ushort CurrentButtons()
        {
            var mouse = Mouse.current;
            ushort buttons = 0;
            if (mouse.leftButton.isPressed) buttons |= 1 << 0;
            if (mouse.rightButton.isPressed) buttons |= 1 << 1;
            if (mouse.middleButton.isPressed) buttons |= 1 << 2;
            return buttons;
        }

        private static ushort ButtonBit(int button)
        {
            return (ushort)(1 << button);
        }

        #endregion
    }
}
