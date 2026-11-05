using CefUnity.Interop;
using Client.Game.InGame.BugReport.WebUiHealth;
using UnityEngine;

namespace Client.WebUiHost.Health
{
    /// <summary>
    /// Webホスト起動の成否とCEFプロセスの最後の状態を記録する。バグ報告は WebUiHealthProbe 経由で読む
    /// Records the web host startup outcome and the last CEF process state; bug reports read them through WebUiHealthProbe
    /// </summary>
    public static class WebUiHealthRecorder
    {
        // CEFのイベントはどのスレッドで来るか保証されないので、状態の読み書きはlockで直列化する
        // CEF events carry no thread guarantee, so state reads and writes are serialized by the lock
        private static readonly object StateLock = new();
        private static WebUiHostStartupState _hostStartup = WebUiHostStartupState.NotAttempted;
        private static CefProcessState _cefProcess = CefProcessState.NotObserved;
        private static bool _observingCefRuntime;

        public static WebUiHostStartupState HostStartup
        {
            get { lock (StateLock) return _hostStartup; }
        }

        public static CefProcessState CefProcess
        {
            get { lock (StateLock) return _cefProcess; }
        }

        public static void RecordHostStartup(WebUiHostStartupState state)
        {
            lock (StateLock) _hostStartup = state;
            Debug.Log($"[WebUiHealth] web host startup: {state}");
        }

        // ブラウザ生成の後に呼ぶ。購読は1回だけ張り、初回でRunningにする（D-11）
        // Call after the browser is created; subscribes once and marks Running on the first call (D-11)
        public static void ObserveCefRuntime()
        {
            lock (StateLock)
            {
                if (_observingCefRuntime) return;
                _observingCefRuntime = true;
            }

            CefRuntime.ServerLost += _ => RecordCefProcess(CefProcessState.LostRestarting);
            CefRuntime.ServerRecovered += _ => RecordCefProcess(CefProcessState.Running);
            CefRuntime.ServerRecoveryFailed += _ => RecordCefProcess(CefProcessState.RecoveryFailed);
            RecordCefProcess(CefProcessState.Running);
        }

        private static void RecordCefProcess(CefProcessState state)
        {
            lock (StateLock)
            {
                if (_cefProcess == state) return;
                _cefProcess = state;
            }

            // 復旧を諦めたCEFは命綱以外のUIを失うので、開発者へ届くLogErrorにする
            // A CEF that gave up recovering loses every UI except the lifeline, so it goes out as a LogError that reaches developers
            if (state == CefProcessState.RecoveryFailed) Debug.LogError($"[WebUiHealth] CEF process: {state}");
            else Debug.Log($"[WebUiHealth] CEF process: {state}");
        }
    }
}
