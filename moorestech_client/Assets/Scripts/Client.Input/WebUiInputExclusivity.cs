using System;
using UniRx;
using UnityEngine;

namespace Client.Input
{
    [System.Flags]
    public enum InputSuppressionScope
    {
        Keyboard = 1,
    }

    /// <summary>
    /// Web UIの状態保持とテキスト入力時のキー抑止を担う。
    /// Holds Web UI input state and suppresses keyboard input while a text field is focused.
    /// </summary>
    public static class WebUiInputExclusivity
    {
        private static readonly object TextInputFocusLock = new();
        private static readonly Subject<bool> TextInputFocusedChangedSubject = new();
        private static volatile bool _pointerOverUi;
        private static volatile bool _textInputFocused;
        private static int _lastKeyboardProbeFrame = -1;

        public static bool IsPointerOverWebUi => _pointerOverUi;
        public static bool IsTextInputFocused => _textInputFocused;

        // 値が変わった時だけ届く。呼び出し元はWebSocketの受信スレッドなので購読側でメインスレッドへ移すこと
        // Fires only on change; callers run on the WebSocket thread, so subscribers must hop to the main thread
        public static IObservable<bool> OnTextInputFocusedChanged => TextInputFocusedChangedSubject;

        public static void SetState(bool pointerOverUi, bool textInputFocused)
        {
            _pointerOverUi = pointerOverUi;

            // 受信ループと切断時リセットが並走しうるため、変化判定と通知を直列化する
            // The receive loop and the disconnect reset can race, so serialize the change check and the notification
            lock (TextInputFocusLock)
            {
                if (_textInputFocused == textInputFocused) return;
                _textInputFocused = textInputFocused;
                TextInputFocusedChangedSubject.OnNext(textInputFocused);
            }
        }

        public static bool IsSuppressed(InputSuppressionScope scope)
        {
            return (scope & InputSuppressionScope.Keyboard) != 0 && _textInputFocused;
        }

        public static void ProbeSuppressed(InputSuppressionScope scope)
        {
            // 入力時だけ記録しログ洪水を防ぐ
            // Log only on input to prevent log floods
            if ((scope & InputSuppressionScope.Keyboard) != 0 && _textInputFocused && _lastKeyboardProbeFrame != Time.frameCount)
            {
                _lastKeyboardProbeFrame = Time.frameCount;
                Debug.Log("[WebUiInputProbe] Suppressed keyboard input because a Web text field owns focus");
            }
        }
    }
}
