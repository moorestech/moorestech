using System.Diagnostics;
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

    /// <summary>
    /// Web UIの状態保持とテキスト入力時のキー抑止を担う。WSが無応答の間はWeb側の値を読まない。
    /// Holds Web UI input state and suppresses keyboard input while a text field is focused; while the WS is silent the web values are not read.
    /// </summary>
    public static class WebUiInputExclusivity
    {
        // Webはpongが15秒途絶えるとWSを閉じる（moorestech_web/webui/src/bridge/transport/webSocketClient.ts の 15000ms）。同じ値で無応答とみなす
        // The web closes the WS after 15s without a pong (15000ms in webSocketClient.ts); the same value marks the web as silent
        private const double WebSilenceThresholdSeconds = 15.0;

        // 現在値と変化通知を1本に畳む。購読が現在値を必ず連れてくるので取得と購読の間に落ちる窓が無い
        // Current value and change notification live in one stream, so nothing can slip between reading and subscribing
        private static readonly ReactiveProperty<bool> TextInputFocusedProperty = new(false);
        private static readonly object WebUiStateLock = new();
        private static volatile bool _pointerOverUi;
        private static volatile bool _webSilent;
        // 未受信は null。時刻の値（0 等）を未受信の印に流用しない。WebUiStateLock の下で読み書きする
        // Null until the first receipt; no time value (such as 0) doubles as never-received. Read and written under WebUiStateLock
        private static long? _lastWebMessageTimestamp;
        private static bool _webTextInputFocused;
        private static int _lastKeyboardProbeFrame = -1;

        // 無応答のWebは値を更新できないので、ポインタ占有を主張させない
        // A silent web cannot update its values, so it never claims the pointer
        public static bool IsPointerOverWebUi => !_webSilent && _pointerOverUi;
        public static bool IsTextInputFocused => TextInputFocusedProperty.Value;

        // 呼び出し元はWebSocketの受信スレッドなので購読側でメインスレッドへ移すこと
        // Callers run on the WebSocket thread, so subscribers must hop to the main thread
        public static IReadOnlyReactiveProperty<bool> TextInputFocused => TextInputFocusedProperty;

        public static void SetState(bool pointerOverUi, bool textInputFocused)
        {
            // 生値は無応答でも保持し、読み出しで印を見る。受信ループと切断時リセット・無応答評価の並走はlockが直列化する
            // Raw values are kept even while silent and the mark is applied on read; the lock serializes the receive loop, disconnect reset and silence evaluation
            lock (WebUiStateLock)
            {
                _pointerOverUi = pointerOverUi;
                _webTextInputFocused = textInputFocused;
                RecomputeTextInputFocused();
            }
        }

        // WSスレッドから受信ごとに呼ぶ。時刻だけ書き、無応答の印は EvaluateWebSilence だけが書く
        // Called on the WS thread per message; it writes only the time and only EvaluateWebSilence writes the silent mark
        public static void RecordWebMessageReceived(long stopwatchTimestamp)
        {
            lock (WebUiStateLock) _lastWebMessageTimestamp = stopwatchTimestamp;
        }

        // 一度も受信していなければ null
        // Null when nothing has ever been received
        public static double? SecondsSinceLastWebMessage(long nowStopwatchTimestamp)
        {
            lock (WebUiStateLock) return SecondsSinceLastWebMessageLocked(nowStopwatchTimestamp);
        }

        private static double? SecondsSinceLastWebMessageLocked(long nowStopwatchTimestamp)
        {
            if (!_lastWebMessageTimestamp.HasValue) return null;
            return (nowStopwatchTimestamp - _lastWebMessageTimestamp.Value) / (double)Stopwatch.Frequency;
        }

        // JSが止まりWSだけ残るとWebの値が古いまま残り、切断時リセットも通らない（plan §3.11）。閾値を超えたらWeb側の値を読まない
        // A stopped JS with a live WS leaves stale web values and skips the disconnect reset (plan §3.11); past the threshold the web side is not read
        public static void EvaluateWebSilence(long nowStopwatchTimestamp)
        {
            // 秒数の算出と印の書き込みを同じlockで行う。分けると間に入った受信を見落とし偽のsilentが立つ
            // Compute the seconds and write the mark under one lock; splitting them misses a receipt in between and raises a false silent
            double? seconds;
            bool silent;
            lock (WebUiStateLock)
            {
                seconds = SecondsSinceLastWebMessageLocked(nowStopwatchTimestamp);
                silent = seconds.HasValue && WebSilenceThresholdSeconds < seconds.Value;
                // silent-ok: 印が変わらない評価は毎秒来るので何もしない / 印の出入りは下のログで開発者が読める
                if (_webSilent == silent) return;
                _webSilent = silent;
                RecomputeTextInputFocused();
            }

            if (silent) Debug.LogWarning($"[WebUiInput] web UI silent for {seconds.Value:F1}s; ignoring web input values until it answers again");
            else Debug.Log("[WebUiInput] web UI answered again; web input values are read again");
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

        // 呼び出し側はWebUiStateLockを保持していること。同値落ちはReactivePropertyが担保する
        // Callers must hold WebUiStateLock; ReactiveProperty drops same-value writes
        private static void RecomputeTextInputFocused()
        {
            TextInputFocusedProperty.Value = !_webSilent && _webTextInputFocused;
        }

#if UNITY_EDITOR
        // 受信時刻と無応答の印は静的に残るため、テスト間で「一度も受信していない」状態へ戻す
        // The receive time and silent mark are static, so tests return to the never-received state between cases
        internal static void ResetWebSilenceForTest()
        {
            lock (WebUiStateLock)
            {
                _lastWebMessageTimestamp = null;
                _webSilent = false;
                RecomputeTextInputFocused();
            }
        }
#endif
    }
}
