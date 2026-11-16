using CefUnity.Interop;
using Client.Game.InGame.BugReport.WebUiHealth;
using UnityEngine;

namespace Client.WebUiHost.Health
{
    /// <summary>
    /// Webホスト起動の成否とCEFの最後の状態を記録
    /// Records web host startup outcome and last CEF state
    /// </summary>
    public static class WebUiHealthRecorder
    {
        // CEFのイベントはどのスレッドで来るか保証されないので、状態の読み書きはlockで直列化する
        // CEF events carry no thread guarantee, so state reads and writes are serialized by the lock
        private static readonly object StateLock = new();
        private static WebUiHostStartupState _hostStartup = WebUiHostStartupState.NotAttempted;
        private static CefProcessState _cefProcess = CefProcessState.NotObserved;
        private static bool _observingCefRuntime;

        internal static WebUiHostStartupState HostStartup
        {
            get { lock (StateLock) return _hostStartup; }
        }

        internal static CefProcessState CefProcess
        {
            get { lock (StateLock) return _cefProcess; }
        }

        public static void RecordHostStartup(WebUiHostStartupState state)
        {
            lock (StateLock) _hostStartup = state;
            Debug.Log($"[WebUiHealth] web host startup: {state}");
        }

        // Webホストが起動しブラウザを生成した後に呼ぶ。購読は1回だけ張り、初回で観測開始を書く。CEF初期化の成否は見ていないので「動作中」とは名乗らない
        // Call after the web host started and the browser was created; subscribes once and records the observation start; CEF init success is not checked, so it never claims "running"
        internal static void ObserveCefRuntime()
        {
            lock (StateLock)
            {
                if (_observingCefRuntime) return;
                _observingCefRuntime = true;
            }

            // 先に観測開始を書く。購読後に書くと、その間に届いたServerLostを上書きして古い値で固まる
            // The observation start is written first; writing it after subscribing could overwrite a ServerLost that arrived in between and freeze a stale value
            RecordCefProcess(CefProcessState.ObservedAfterBrowserStart);
            CefRuntime.ServerLost += _ => RecordCefProcess(CefProcessState.LostRestarting);
            // 復旧通知の後も喪失前と同じく「観測中で喪失通知なし」に戻る
            // After a recovery notice the state returns to "observed with no loss notice", as before the loss
            CefRuntime.ServerRecovered += _ => RecordCefProcess(CefProcessState.ObservedAfterBrowserStart);
            CefRuntime.ServerRecoveryFailed += _ => RecordCefProcess(CefProcessState.RecoveryFailed);
        }

        private static void RecordCefProcess(CefProcessState state)
        {
            lock (StateLock)
            {
                if (_cefProcess == state) return;
                _cefProcess = state;
            }

            // 復旧を諦めたCEFはWeb UIをすべて失うので、開発者へ届くLogErrorにする
            // A CEF that gave up recovering loses the whole web UI, so it goes out as a LogError that reaches developers
            if (state == CefProcessState.RecoveryFailed) Debug.LogError($"[WebUiHealth] CEF process: {state}");
            else Debug.Log($"[WebUiHealth] CEF process: {state}");
        }
    }
}
