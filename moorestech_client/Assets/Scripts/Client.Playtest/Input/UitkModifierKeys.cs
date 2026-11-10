using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Client.Playtest.Input
{
    /// <summary>
    ///     注入した修飾キーを evt.shiftKey へ届ける
    ///     Delivers injected modifier keys to evt.shiftKey
    ///
    ///     UITK（EventSystem 経由の PanelEventHandler）は修飾キーを IMGUI の Event 列から静的値 s_Modifiers に写し、ポインタ事象の modifiers に使う。
    ///     QueueStateEvent の注入は IMGUI の列に入らないので、押下中の修飾キーをその静的値へ置き、毎フレームの入力更新の後にも置き直す（実キーの事象で上書きされても次のフレームで戻す）。
    ///     本番は実キーが IMGUI の列に入るのでこの口は要らない（DSL 専用）。
    ///     UITK (PanelEventHandler via EventSystem) copies modifiers from the IMGUI Event queue into the static s_Modifiers and stamps pointer events with it.
    ///     QueueStateEvent injection never reaches the IMGUI queue, so held modifiers are written into that static and rewritten after every input update (restoring it if a real event overwrote it).
    ///     Production does not need this since real keys reach the IMGUI queue (DSL only).
    /// </summary>
    public static class UitkModifierKeys
    {
        private static FieldInfo _modifiersField;
        private static EventModifiers _held = EventModifiers.None;
        private static bool _hooked;
        private static readonly HashSet<IPanel> _watchedPanels = new();

        public static void Apply(IReadOnlyCollection<Key> pressedKeys)
        {
            // 修飾キーの組を作り、変化時だけ置く
            // Build the modifier set and write it only on change
            var held = EventModifiers.None;
            foreach (var key in pressedKeys) held |= ToModifier(key);
            if (held == _held) return;
            _held = held;
            Write(held);

            // 押下中だけ毎フレーム置き直す
            // Rewrite every frame only while held
            if (held != EventModifiers.None && !_hooked) InputSystem.onAfterUpdate += Rewrite;
            if (held == EventModifiers.None && _hooked) InputSystem.onAfterUpdate -= Rewrite;
            _hooked = held != EventModifiers.None;

            #region Internal

            EventModifiers ToModifier(Key key)
            {
                return key switch
                {
                    Key.LeftShift or Key.RightShift => EventModifiers.Shift,
                    Key.LeftCtrl or Key.RightCtrl => EventModifiers.Control,
                    Key.LeftAlt or Key.RightAlt => EventModifiers.Alt,
                    Key.LeftCommand or Key.RightCommand => EventModifiers.Command,
                    _ => EventModifiers.None,
                };
            }

            #endregion
        }

        public static void WatchDelivery()
        {
            // 修飾値が実際に読まれる PointerDown で照合する。注入時に読むと Rewrite の前後で誤検出・見逃しが出る
            // Check at PointerDown where modifiers are actually read; checking at injection misfires around Rewrite
            foreach (var document in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var panel = document.rootVisualElement?.panel;
                if (panel == null || !_watchedPanels.Add(panel)) continue;
                panel.visualTree.RegisterCallback<PointerDownEvent>(ReportDroppedModifiers, TrickleDown.TrickleDown);
            }
        }

        private static void ReportDroppedModifiers(PointerDownEvent evt)
        {
            // 押下中の修飾キーが外れて届いたら、実入力の上書きなのでログへ出す（Editor 前面で人が触れた可能性）
            // If held modifiers arrive missing, a real input overwrote them, so log it (someone may have touched the focused Editor)
            if (_held == EventModifiers.None || (evt.modifiers & _held) == _held) return;
            Debug.LogError($"[UitkModifierKeys] 注入した修飾キー {_held} が実入力で上書きされ、PointerDown に {evt.modifiers} で届いた（evt.shiftKey 等が外れる）");
        }

        private static void Rewrite()
        {
            Write(_held);
        }

        private static void Write(EventModifiers modifiers)
        {
            ModifiersField().SetValue(null, modifiers);
        }

        private static FieldInfo ModifiersField()
        {
            // 非公開の静的値なので名前で引く。Unity の更新で消えたら DSL の Shift が黙って効かなくなるので止める
            // A private static, so look it up by name; stop if a Unity update removed it, since DSL Shift would otherwise silently fail
            if (_modifiersField != null) return _modifiersField;
            var handler = Type.GetType("UnityEngine.UIElements.PanelEventHandler, UnityEngine.UI");
            _modifiersField = handler?.GetField("s_Modifiers", BindingFlags.NonPublic | BindingFlags.Static);
            if (_modifiersField == null) throw new InvalidOperationException("[UitkModifierKeys] PanelEventHandler.s_Modifiers が見つからない（Unity の版で内部名が変わった）ので DSL の修飾キーを UITK へ届けられない");
            return _modifiersField;
        }
    }
}
