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
        private static readonly object WebUiStateLock = new();
        private static volatile bool _pointerOverUi;
        private static volatile bool _webUiSuspended;
        private static int _lastKeyboardProbeFrame = -1;

        // 休止中のWebは描画も受け手も無いので、ポインタ占有を主張させない
        // A suspended web UI has no renderer or receiver, so it never claims the pointer
        public static bool IsPointerOverWebUi => !_webUiSuspended && _pointerOverUi;
        public static bool IsTextInputFocused => TextInputFocusedProperty.Value;

        // 呼び出し元はWebSocketの受信スレッドなので購読側でメインスレッドへ移すこと
        // Callers run on the WebSocket thread, so subscribers must hop to the main thread
        public static IReadOnlyReactiveProperty<bool> TextInputFocused => TextInputFocusedProperty;

        public static void SetState(bool pointerOverUi, bool textInputFocused)
        {
            // 同値落ちはReactivePropertyが担保し、受信ループと切断時リセット・休止切替の並走はlockが直列化する
            // ReactiveProperty drops same-value writes while the lock serializes the receive loop against the disconnect reset and suspension
            lock (WebUiStateLock)
            {
                // 休止中に届いた値はunmount前の残りなので捨てる
                // Values arriving while suspended are leftovers from before the unmount, so drop them
                _pointerOverUi = !_webUiSuspended && pointerOverUi;
                TextInputFocusedProperty.Value = !_webUiSuspended && textInputFocused;
            }
        }

        // UITKが主パネルを出す間Webを全面休止する（R1）。切替の前後とも古い値を捨て、Webの次の送信を待つ
        // Suspend the whole web UI while UITK shows a main panel (R1); drop stale values both ways and wait for the web's next send
        public static void SetWebUiSuspended(bool suspended)
        {
            lock (WebUiStateLock)
            {
                if (_webUiSuspended == suspended) return;
                _webUiSuspended = suspended;

                // 購読者にも false を届けるためReactiveProperty経由で落とす
                // Clear through the ReactiveProperty so subscribers also receive false
                _pointerOverUi = false;
                TextInputFocusedProperty.Value = false;
            }
            Debug.Log($"[WebUiInput] web UI suspended={suspended}");
        }

        public static bool IsSuppressed(InputSuppressionScope scope)
        {
            return (scope & InputSuppressionScope.Keyboard) != 0 && !_webUiSuspended && TextInputFocusedProperty.Value;
        }

        public static void ProbeSuppressed(InputSuppressionScope scope)
        {
            // 入力時だけ記録しログ洪水を防ぐ
            // Log only on input to prevent log floods
            if ((scope & InputSuppressionScope.Keyboard) != 0 && !_webUiSuspended && TextInputFocusedProperty.Value && _lastKeyboardProbeFrame != Time.frameCount)
            {
                _lastKeyboardProbeFrame = Time.frameCount;
                Debug.Log("[WebUiInputProbe] Suppressed keyboard input because a Web text field owns focus");
            }
        }
    }
}
