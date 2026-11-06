using System.Diagnostics;
using System.Threading;
using UniRx;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Client.Input
{
    [System.Flags]
    public enum InputSuppressionScope
    {
        Keyboard = 1,
    }

    // 全体スイッチで選ばれた描く系。長命ブランチの中だけの区別で、切替 PR で消す（plan §3.3 R1）
    // The drawing system chosen by the global switch; it exists only on the long-lived branch and goes away in the cutover PR (plan §3.3 R1)
    public enum UiSystem
    {
        Web,
        GameUi,
    }

    // 全体スイッチで選ばれた系の入力占有だけを読み、テキスト入力時のキー抑止とESCラッチを担う（plan §3.3 R2）
    // Reads input occupancy only from the system chosen by the global switch, suppresses keyboard input during text entry and latches ESC (plan §3.3 R2)
    public static class UiInputExclusivity
    {
        // Webはpongが15秒途絶えるとWSを閉じる（moorestech_web/webui/src/bridge/transport/webSocketClient.ts の 15000ms）。同じ値で無応答とみなす
        // The web closes the WS after 15s without a pong (15000ms in webSocketClient.ts); the same value marks the web as silent
        private const double WebSilenceThresholdSeconds = 15.0;

        // 現在値と変化通知を1本に畳む。購読が現在値を必ず連れてくるので取得と購読の間に落ちる窓が無い
        // Current value and change notification live in one stream, so nothing can slip between reading and subscribing
        private static readonly ReactiveProperty<bool> TextInputFocusedProperty = new(false);
        private static readonly object UiStateLock = new();
        private static volatile UiSystem _activeUiSystem = UiSystem.Web;
        private static volatile bool _pointerOverUi;
        private static volatile bool _webSilent;
        private static long _lastWebMessageTimestamp;
        private static bool _webTextInputFocused;
        private static volatile bool _gameUiTextInputFocused;
        private static IGameUiPointerProbe _gameUiPointerProbe;
        private static int _escapeConsumedFrame = -1;
        private static int _lastKeyboardProbeFrame = -1;

        // 選ばれていない系の値は読まない。無応答のWebは値を更新できないのでポインタ占有を主張させない
        // The unselected system is never read, and a silent web cannot update its values so it never claims the pointer
        public static bool IsPointerOverWebUi => _activeUiSystem == UiSystem.Web && !_webSilent && _pointerOverUi;
        public static UiSystem ActiveUiSystem => _activeUiSystem;
        public static bool IsTextInputFocused => TextInputFocusedProperty.Value;
        public static bool IsEscapeConsumedThisFrame => _escapeConsumedFrame == Time.frameCount;
        // 呼び出し元はWebSocketの受信スレッドなので購読側でメインスレッドへ移すこと
        // Callers run on the WebSocket thread, so subscribers must hop to the main thread
        public static IReadOnlyReactiveProperty<bool> TextInputFocused => TextInputFocusedProperty;

        // 起動で1回だけ composition root（Client.Starter）が押し込む。internal で他の層から書かせない
        // Pushed once per boot by the composition root (Client.Starter); internal so no other layer can write it
        internal static void SetActiveUiSystem(UiSystem system)
        {
            lock (UiStateLock)
            {
                if (_activeUiSystem == system) return;
                _activeUiSystem = system;
                RecomputeTextInputFocused();
            }
            Debug.Log($"[UiInput] active UI system={system}");
        }

        public static void SetState(bool pointerOverUi, bool textInputFocused)
        {
            // 同値落ちはReactivePropertyが担保し、受信ループと切断時リセットの並走はlockが直列化する
            // ReactiveProperty drops same-value writes while the lock serializes the receive loop against the disconnect reset
            lock (UiStateLock)
            {
                _pointerOverUi = pointerOverUi;
                _webTextInputFocused = textInputFocused;
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

        // null で解除。系の区別は _activeUiSystem が持ち、null は GameUi の系で DI が登録する前だけを表す
        // Pass null to unregister; the system lives in _activeUiSystem and null only means GameUi before DI registers the probe
        public static void SetGameUiPointerProbe(IGameUiPointerProbe probe)
        {
            _gameUiPointerProbe = probe;
        }

        // GameUi の系のときだけ Pick を見る（R2）
        // Look at the pick only in the GameUi system (R2)
        public static bool IsPointerOverGameUi(Vector2 screenPosition)
        {
            var probe = _gameUiPointerProbe;
            return _activeUiSystem == UiSystem.GameUi && probe != null && probe.IsPointerOverGameUi(screenPosition);
        }

        // 選ばれた系のポインタ占有。両系の論理和にしない（R2）
        // Pointer occupancy of the chosen system; never the OR of both systems (R2)
        public static bool IsPointerOverActiveUi(Vector2 screenPosition)
        {
            return _activeUiSystem == UiSystem.GameUi ? IsPointerOverGameUi(screenPosition) : IsPointerOverWebUi;
        }

        // このフレームのESCはUIが使った。InputKey/HybridInputは同フレームの押下を返さない
        // This frame's ESC was used by the UI; InputKey/HybridInput return no press for it within the same frame
        public static void ConsumeEscapeThisFrame()
        {
            if (IsEscapeConsumedThisFrame) return;
            _escapeConsumedFrame = Time.frameCount;
            Debug.Log($"[UiInput] escape consumed by game UI at frame {_escapeConsumedFrame}");
        }

        // WSスレッドから受信ごとに呼ぶ。時刻だけ書き、無応答の印は EvaluateWebSilence だけが書く（D-22）
        // Called on the WS thread per message; it writes only the time and only EvaluateWebSilence writes the silent mark (D-22)
        public static void RecordWebMessageReceived(long stopwatchTimestamp)
        {
            Interlocked.Exchange(ref _lastWebMessageTimestamp, stopwatchTimestamp);
        }

        // 一度も受信していなければ null（Web の系の起動直後。バグ報告の診断も読む）
        // Null when nothing has ever been received (right after a Web boot; the bug-report diagnostics read it too)
        public static double? SecondsSinceLastWebMessage(long nowStopwatchTimestamp)
        {
            var last = Interlocked.Read(ref _lastWebMessageTimestamp);
            if (last == 0) return null;
            return (nowStopwatchTimestamp - last) / (double)Stopwatch.Frequency;
        }

        // JSが止まりWSだけ残るとWebの値が古いまま残る（§3.11）。閾値超えでWeb側を読まない。GameUiの系では系の印で判定しない
        // A stopped JS with a live WS leaves stale web values (§3.11); past the threshold the web side is not read, and the GameUi system never judges silence
        public static void EvaluateWebSilence(long nowStopwatchTimestamp)
        {
            var seconds = SecondsSinceLastWebMessage(nowStopwatchTimestamp);
            var silent = _activeUiSystem == UiSystem.Web && seconds.HasValue && WebSilenceThresholdSeconds < seconds.Value;
            lock (UiStateLock)
            {
                if (_webSilent == silent) return;
                _webSilent = silent;
                RecomputeTextInputFocused();
            }

            if (silent) Debug.LogWarning($"[UiInput] web UI silent for {seconds.Value:F1}s; ignoring web input values until it answers again");
            else Debug.Log("[UiInput] web UI answered again; web input values are read again");
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
                Debug.Log($"[UiInputProbe] Suppressed keyboard input because a {_activeUiSystem} text field owns focus");
            }
        }

        // 呼び出し側はUiStateLockを保持していること。選ばれた系の値だけを読む（R2）
        // Callers must hold UiStateLock; only the chosen system's value is read (R2)
        private static void RecomputeTextInputFocused()
        {
            TextInputFocusedProperty.Value = _activeUiSystem == UiSystem.GameUi ? _gameUiTextInputFocused : !_webSilent && _webTextInputFocused;
        }

#if UNITY_EDITOR
        // EditModeではTime.frameCountが進まずラッチがテスト間で残るため、テストから解除する
        // In EditMode Time.frameCount does not advance so the latch would leak between tests; tests clear it here
        internal static void ClearEscapeLatchForTest()
        {
            _escapeConsumedFrame = -1;
        }

        // 受信時刻と無応答の印は静的に残るため、テスト間で「一度も受信していない」状態へ戻す
        // The receive time and silent mark are static, so tests return to the never-received state between cases
        internal static void ResetWebSilenceForTest()
        {
            Interlocked.Exchange(ref _lastWebMessageTimestamp, 0);
            lock (UiStateLock)
            {
                _webSilent = false;
                RecomputeTextInputFocused();
            }
        }
#endif
    }
}
