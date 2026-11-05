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
    /// Web UIとGameUi(UITK)の入力占有状態を論理和で保持し、テキスト入力時のキー抑止とESCラッチを担う。
    /// Holds input-occupancy state of Web UI and GameUi (UITK) as a logical OR, suppresses keyboard input during text entry and latches ESC.
    /// </summary>
    public static class UiInputExclusivity
    {
        // 現在値と変化通知を1本に畳む。購読が現在値を必ず連れてくるので取得と購読の間に落ちる窓が無い
        // Current value and change notification live in one stream, so nothing can slip between reading and subscribing
        private static readonly ReactiveProperty<bool> TextInputFocusedProperty = new(false);
        private static readonly object UiStateLock = new();
        private static volatile bool _pointerOverUi;
        private static volatile bool _webUiSuspended;
        private static bool _webTextInputFocused;
        private static volatile bool _gameUiTextInputFocused;
        private static IGameUiPointerProbe _gameUiPointerProbe;
        private static int _escapeConsumedFrame = -1;
        private static int _lastKeyboardProbeFrame = -1;

        // 休止中のWebは描画も受け手も無いので、ポインタ占有を主張させない
        // A suspended web UI has no renderer or receiver, so it never claims the pointer
        public static bool IsPointerOverWebUi => !_webUiSuspended && _pointerOverUi;
        public static bool IsTextInputFocused => TextInputFocusedProperty.Value;
        public static bool IsEscapeConsumedThisFrame => _escapeConsumedFrame == Time.frameCount;

        // 呼び出し元はWebSocketの受信スレッドなので購読側でメインスレッドへ移すこと
        // Callers run on the WebSocket thread, so subscribers must hop to the main thread
        public static IReadOnlyReactiveProperty<bool> TextInputFocused => TextInputFocusedProperty;

        public static void SetState(bool pointerOverUi, bool textInputFocused)
        {
            // 同値落ちはReactivePropertyが担保し、受信ループと切断時リセット・休止切替の並走はlockが直列化する
            // ReactiveProperty drops same-value writes while the lock serializes the receive loop against the disconnect reset and suspension
            lock (UiStateLock)
            {
                // 休止中に届いた値はunmount前の残りなので捨てる
                // Values arriving while suspended are leftovers from before the unmount, so drop them
                _pointerOverUi = !_webUiSuspended && pointerOverUi;
                _webTextInputFocused = !_webUiSuspended && textInputFocused;
                RecomputeTextInputFocused();
            }
        }

        // GameUiの入力欄のフォーカス。メインスレッドから呼ぶが、Webの書き込みと並走するのでlockの中で再計算する
        // GameUi text-field focus; called on the main thread but recomputed under the lock because web writes run concurrently
        public static void SetGameUiTextInputFocused(bool focused)
        {
            lock (UiStateLock)
            {
                _gameUiTextInputFocused = focused;
                RecomputeTextInputFocused();
            }
        }

        // null で解除。未登録（GameUi無し）はポインタ占有なし
        // Pass null to unregister; with no probe (no GameUi) the pointer is never occupied
        public static void SetGameUiPointerProbe(IGameUiPointerProbe probe)
        {
            _gameUiPointerProbe = probe;
        }

        public static bool IsPointerOverGameUi(Vector2 screenPosition)
        {
            var probe = _gameUiPointerProbe;
            return probe != null && probe.IsPointerOverGameUi(screenPosition);
        }

        // このフレームのESCはUIが使った。InputKey/HybridInputは同フレームの押下を返さない
        // This frame's ESC was used by the UI; InputKey/HybridInput return no press for it within the same frame
        public static void ConsumeEscapeThisFrame()
        {
            if (IsEscapeConsumedThisFrame) return;
            _escapeConsumedFrame = Time.frameCount;
            Debug.Log($"[UiInput] escape consumed by game UI at frame {_escapeConsumedFrame}");
        }

        // UITKが主パネルを出す間Webを全面休止する（R1）。切替の前後とも古い値を捨て、Webの次の送信を待つ
        // Suspend the whole web UI while UITK shows a main panel (R1); drop stale values both ways and wait for the web's next send
        public static void SetWebUiSuspended(bool suspended)
        {
            lock (UiStateLock)
            {
                if (_webUiSuspended == suspended) return;
                _webUiSuspended = suspended;

                // 購読者にも false を届けるためReactiveProperty経由で落とす。GameUi側の値は休止に影響されない
                // Clear through the ReactiveProperty so subscribers also receive false; the GameUi value is unaffected by suspension
                _pointerOverUi = false;
                _webTextInputFocused = false;
                RecomputeTextInputFocused();
            }
            Debug.Log($"[UiInput] web UI suspended={suspended}");
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
                var owner = _gameUiTextInputFocused ? "GameUi" : "Web";
                Debug.Log($"[UiInputProbe] Suppressed keyboard input because a {owner} text field owns focus");
            }
        }

        // 呼び出し側はUiStateLockを保持していること
        // Callers must hold UiStateLock
        private static void RecomputeTextInputFocused()
        {
            TextInputFocusedProperty.Value = _gameUiTextInputFocused || (!_webUiSuspended && _webTextInputFocused);
        }

#if UNITY_EDITOR
        // EditModeではTime.frameCountが進まずラッチがテスト間で残るため、テストから解除する
        // In EditMode Time.frameCount does not advance so the latch would leak between tests; tests clear it here
        internal static void ClearEscapeLatchForTest()
        {
            _escapeConsumedFrame = -1;
        }
#endif
    }
}
