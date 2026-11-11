using System;
using System.Collections.Generic;
using Client.Input;
using Client.Ui.Foundation.Root;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Client.Playtest.Input
{
    /// <summary>
    ///     注入したキーの押下・解放を、GameUi でフォーカスを持つ要素へ UITK のキー事象として届ける
    ///     Delivers injected key presses and releases to the focused GameUi element as UITK key events
    ///
    ///     EventSystem 経由の UITK はキー事象を IMGUI の Event 列（OS のキー）からだけ作り、UI 専用のアクション資産には Cancel・Navigate が無い。
    ///     よって QueueStateEvent の注入キーは UITK に一切届かず、入力欄の ESC（GameUiInputRules の KeyDown）を録画で駆動できない。
    ///     実キーと同じフレームに届くよう、Input System がその押下を処理した直後（onAfterUpdate で wasPressedThisFrame）に送る。本番は実キーが IMGUI の列に入るのでこの口は要らない（DSL 専用）。
    ///     UITK through the EventSystem builds key events only from the IMGUI Event queue (OS keys), and the UI-only action asset has no Cancel or Navigate.
    ///     So keys injected with QueueStateEvent never reach UITK, and a text field's ESC (GameUiInputRules KeyDown) cannot be driven in recordings.
    ///     To land in the same frame as a real key, send right after the Input System processed it (wasPressedThisFrame in onAfterUpdate). Production does not need this since real keys reach the IMGUI queue (DSL only).
    /// </summary>
    public static class UitkFocusedKeyEvents
    {
        private static readonly HashSet<Key> Held = new();
        private static readonly List<(Key key, bool down)> Pending = new();
        private static bool _hooked;

        public static void Apply(IReadOnlyCollection<Key> pressedKeys)
        {
            // 前回との差分を押下・解放の事象として積む
            // Queue the difference from last time as press and release events
            foreach (var key in pressedKeys)
            {
                if (Held.Add(key)) Pending.Add((key, true));
            }
            Held.RemoveWhere(key =>
            {
                var released = !Contains(pressedKeys, key);
                if (released) Pending.Add((key, false));
                return released;
            });
            if (Pending.Count == 0 || _hooked) return;
            InputSystem.onAfterUpdate += DeliverProcessed;
            _hooked = true;
        }

        private static void DeliverProcessed()
        {
            // Input System が処理した分だけ送り、残りは次の更新で待つ
            // Send only what the Input System has processed; the rest waits for the next update
            for (var i = Pending.Count - 1; 0 <= i; i--)
            {
                var (key, down) = Pending[i];
                var control = Keyboard.current[key];
                if (down ? !control.wasPressedThisFrame : !control.wasReleasedThisFrame) continue;
                Pending.RemoveAt(i);
                Send(key, down);
            }
            if (0 < Pending.Count) return;
            InputSystem.onAfterUpdate -= DeliverProcessed;
            _hooked = false;
        }

        private static void Send(Key key, bool down)
        {
            // silent-ok: Web 系・GameUi 未生成・フォーカス無しでは実キーも入力欄へ届かない / 入力欄の ESC は録画の Assert（モーダルが閉じる）が見る
            if (UiInputExclusivity.ActiveUiSystem != UiSystem.GameUi || GameUiRoot.Instance == null) return;
            var panel = GameUiRoot.Instance.GetRootElement().panel;
            if (panel?.focusController.focusedElement is not VisualElement focused) return;

            // silent-ok: KeyCode に写せないキー（修飾キー等）は UitkModifierKeys が別経路で届ける / その経路の LogError が欠落を知らせる
            if (!TryToKeyCode(key, out var keyCode)) return;
            using EventBase evt = down ? KeyDownEvent.GetPooled('\0', keyCode, EventModifiers.None) : KeyUpEvent.GetPooled('\0', keyCode, EventModifiers.None);
            evt.target = focused;
            panel.visualTree.SendEvent(evt);
        }

        // Input System の Key 名を KeyCode 名へ写す（名前の違う Enter と数字だけ個別に写す）
        // Map Input System Key names to KeyCode names (only Enter and digits differ by name)
        private static bool TryToKeyCode(Key key, out KeyCode keyCode)
        {
            if (key == Key.Enter) { keyCode = KeyCode.Return; return true; }
            if (Key.Digit1 <= key && key <= Key.Digit9) { keyCode = KeyCode.Alpha1 + (key - Key.Digit1); return true; }
            if (key == Key.Digit0) { keyCode = KeyCode.Alpha0; return true; }
            return Enum.TryParse(key.ToString(), out keyCode) && keyCode != KeyCode.None && !IsModifier(keyCode);
        }

        private static bool IsModifier(KeyCode keyCode)
        {
            return keyCode is KeyCode.LeftShift or KeyCode.RightShift or KeyCode.LeftControl or KeyCode.RightControl or KeyCode.LeftAlt or KeyCode.RightAlt or KeyCode.LeftCommand or KeyCode.RightCommand;
        }

        private static bool Contains(IReadOnlyCollection<Key> keys, Key key)
        {
            foreach (var each in keys)
            {
                if (each == key) return true;
            }
            return false;
        }
    }
}
