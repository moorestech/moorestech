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
        // 現在値と変化通知を1本に畳む。購読が現在値を必ず連れてくるので取得と購読の間に落ちる窓が無い
        // Current value and change notification live in one stream, so nothing can slip between reading and subscribing
        private static readonly ReactiveProperty<bool> TextInputFocusedProperty = new(false);
        private static readonly object TextInputFocusLock = new();
        private static volatile bool _pointerOverUi;
        private static int _lastKeyboardProbeFrame = -1;

        public static bool IsPointerOverWebUi => _pointerOverUi;
        public static bool IsTextInputFocused => TextInputFocusedProperty.Value;

        // 呼び出し元はWebSocketの受信スレッドなので購読側でメインスレッドへ移すこと
        // Callers run on the WebSocket thread, so subscribers must hop to the main thread
        public static IReadOnlyReactiveProperty<bool> TextInputFocused => TextInputFocusedProperty;

        public static void SetState(bool pointerOverUi, bool textInputFocused)
        {
            _pointerOverUi = pointerOverUi;

            // 同値落ちはReactivePropertyが担保し、受信ループと切断時リセットの並走はlockが直列化する
            // ReactiveProperty drops same-value writes while the lock serializes the receive loop against the disconnect reset
            lock (TextInputFocusLock)
            {
                TextInputFocusedProperty.Value = textInputFocused;
            }
        }

        public static bool IsSuppressed(InputSuppressionScope scope)
        {
            return (scope & InputSuppressionScope.Keyboard) != 0 && TextInputFocusedProperty.Value;
        }

        public static void ProbeSuppressed(InputSuppressionScope scope)
        {
            // 入力時だけ記録しログ洪水を防ぐ
            // Log only on input to prevent log floods
            if ((scope & InputSuppressionScope.Keyboard) != 0 && TextInputFocusedProperty.Value && _lastKeyboardProbeFrame != Time.frameCount)
            {
                _lastKeyboardProbeFrame = Time.frameCount;
                Debug.Log("[WebUiInputProbe] Suppressed keyboard input because a Web text field owns focus");
            }
        }
    }
}
