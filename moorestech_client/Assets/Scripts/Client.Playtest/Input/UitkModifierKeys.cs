using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Playtest.Input
{
    /// <summary>
    ///     注入した修飾キーを UITK の evt.shiftKey 等へ届ける
    ///     Delivers injected modifier keys to UITK's evt.shiftKey and friends
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

        public static void Apply(IReadOnlyCollection<Key> pressedKeys)
        {
            // 押下中のキーから修飾キーの組を作り、変われば静的値へ置く
            // Build the modifier set from held keys and write it when it changed
            var held = EventModifiers.None;
            foreach (var key in pressedKeys) held |= ToModifier(key);
            if (held == _held) return;
            _held = held;
            Write(held);

            // 押下中だけ毎フレーム置き直す（離したら外す）
            // Rewrite every frame only while held (unhook on release)
            if (held != EventModifiers.None && !_hooked) InputSystem.onAfterUpdate += Rewrite;
            if (held == EventModifiers.None && _hooked) InputSystem.onAfterUpdate -= Rewrite;
            _hooked = held != EventModifiers.None;
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

        private static EventModifiers ToModifier(Key key)
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
    }
}
