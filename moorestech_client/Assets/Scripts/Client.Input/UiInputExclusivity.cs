using UniRx;
using UnityEngine;

namespace Client.Input
{
    // UITK（GameUi）の入力占有を 1 か所に持つ。切替後は描く系が UITK だけ
    // Holds the UITK (GameUi) input occupancy in one place; after the cutover UITK is the only drawing system
    public static class UiInputExclusivity
    {
        // 現在値と変化通知を1本に畳む。購読が現在値を必ず連れてくるので取得と購読の間に落ちる窓が無い
        // Current value and change notification live in one stream, so nothing can slip between reading and subscribing
        private static readonly ReactiveProperty<bool> TextInputFocusedProperty = new(false);
        private static IGameUiPointerProbe _gameUiPointerProbe;
        private static int _escapeConsumedFrame = -1, _escapeBlurredTextFieldFrame = -1;
        private static int _lastKeyboardProbeFrame = -1;

        public static bool IsTextInputFocused => TextInputFocusedProperty.Value;
        public static bool IsEscapeConsumedThisFrame => _escapeConsumedFrame == Time.frameCount;
        // 書き手は GameUi の入力欄だけで、メインスレッドから届く
        // The only writer is the GameUi text field, on the main thread
        public static IReadOnlyReactiveProperty<bool> TextInputFocused => TextInputFocusedProperty;

        // 同値の書き込みは ReactiveProperty が落とす
        // ReactiveProperty drops same-value writes
        public static void SetGameUiTextInputFocused(bool focused)
        {
            TextInputFocusedProperty.Value = focused;
        }

        // null で解除。null は DI が登録する前だけ
        // Pass null to unregister; null only means before DI registers the probe
        public static void SetGameUiPointerProbe(IGameUiPointerProbe probe)
        {
            _gameUiPointerProbe = probe;
        }

        public static bool IsPointerOverGameUi(Vector2 screenPosition)
        {
            var probe = _gameUiPointerProbe;
            // silent-ok: DI の前は UITK のパネルが無く乗り得ない / 登録漏れは GameUiPointerWiringChecks（L3）が実ゲームで落とす
            if (probe == null) return false;
            return probe.IsPointerOverGameUi(screenPosition);
        }

        // このフレームのESCはUIが使った。InputKey/HybridInputは同フレームの押下を返さない
        // This frame's ESC was used by the UI; InputKey/HybridInput return no press for it within the same frame
        public static void ConsumeEscapeThisFrame()
        {
            if (IsEscapeConsumedThisFrame) return;
            _escapeConsumedFrame = Time.frameCount;
            Debug.Log($"[UiInput] escape consumed by game UI at frame {_escapeConsumedFrame}");
        }

        // 変換中でない ESC で入力欄を外したフレームの印。変換中の ESC では立てない（モーダルの ESC 取消が読む）
        // Marks the frame an ESC outside IME composition blurred a text field; never set during composition (read by the modal's ESC cancel)
        public static bool IsEscapeBlurredTextFieldThisFrame => _escapeBlurredTextFieldFrame == Time.frameCount;
        public static void MarkEscapeBlurredTextFieldThisFrame() => _escapeBlurredTextFieldFrame = Time.frameCount;

        public static bool IsSuppressed(InputSuppressionScope scope) => (scope & InputSuppressionScope.Keyboard) != 0 && TextInputFocusedProperty.Value;

        public static void ProbeSuppressed(InputSuppressionScope scope)
        {
            // 抑止の判定は IsSuppressed の 1 か所。入力時だけ記録しログ洪水を防ぐ
            // The suppression check lives only in IsSuppressed; log only on input to prevent log floods
            if (IsSuppressed(scope) && _lastKeyboardProbeFrame != Time.frameCount)
            {
                _lastKeyboardProbeFrame = Time.frameCount;
                Debug.Log("[UiInputProbe] Suppressed keyboard input because a GameUi text field owns focus");
            }
        }
#if UNITY_EDITOR
        // EditModeではTime.frameCountが進まずラッチがテスト間で残るため、テストから解除する
        // In EditMode Time.frameCount does not advance so the latch would leak between tests; tests clear it here
        internal static void ClearEscapeLatchForTest()
        {
            _escapeConsumedFrame = _escapeBlurredTextFieldFrame = -1;
        }
#endif
    }
}
