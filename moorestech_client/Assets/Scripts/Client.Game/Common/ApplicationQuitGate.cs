using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.Common
{
    /// <summary>
    /// OS由来の終了要求を止め、書き出しを待ってからアプリを落とす関所
    /// The gate that holds OS-originated quit requests and closes the app only after the flush
    /// </summary>
    internal static class ApplicationQuitGate
    {
        private static bool _deferralInstalled;
        private static bool _quitInProgress;
        private static bool _quitAllowed;

        internal static void InstallDeferral()
        {
            // Editorの終了要求を止めるとEditor自体が閉じられなくなる。Editorでの停止は待たずに InstallUnannouncedExitNotice の保険が記録する
            // Holding the Editor's own quit would keep the Editor from closing; an Editor stop is instead recorded, without waiting, by InstallUnannouncedExitNotice
            if (Application.isEditor || _deferralInstalled) return;
            _deferralInstalled = true;
            Application.wantsToQuit += OnApplicationWantsToQuit;
        }

        internal static void ResetForNewSession()
        {
            _quitInProgress = false;
            _quitAllowed = false;
        }

        // wantsToQuit の購読者。戻り値 false が「今は閉じない」、true が「閉じてよい」
        // The wantsToQuit subscriber; false means "do not close now", true means "closing is allowed"
        internal static bool OnApplicationWantsToQuit()
        {
            if (_quitAllowed) return true;
            if (_quitInProgress)
            {
                Debug.Log("終了処理の書き出し中のため、重ねて来た終了要求は保留します");
                return false;
            }
            Debug.Log("終了要求を保留し、書き出しの完了を待ってから終了します");
            _quitInProgress = true;
            QuitAfterCallbackReturnsAsync().Forget(LogFailureAndAllowQuit);
            return false;

            #region Internal

            // wantsToQuit の中で呼んだ Application.Quit は、直後に返す false で取り消される。参加者が居ない（ロード中）と書き出しが同期で終わり中で呼んでしまうため、1フレーム出てから終了口へ流す
            // An Application.Quit issued inside wantsToQuit is cancelled by the false returned right after; with no participant (still loading) the flush completes synchronously and would call it inside, so leave the callback for a frame first
            static async UniTask QuitAfterCallbackReturnsAsync()
            {
                await UniTask.Yield();
                await QuitAfterFlushAsync(GameShutdownReason.IntentionalExit);
            }

            // 終了口が例外で抜けても保留を残さない。残すと以後の×ボタンがすべて保留され閉じなくなる
            // Never leave the hold behind when the exit path throws; otherwise every later close button is held and the app never closes
            static void LogFailureAndAllowQuit(System.Exception exception)
            {
                GameShutdownEvent.LogShutdownFailure(exception);
                Debug.LogError("終了処理が例外で止まったため、書き出しの完了を待たずにアプリを終了します");
                AllowQuitAndExit();
            }

            #endregion
        }

        internal static async UniTask QuitAfterFlushAsync(GameShutdownReason reason)
        {
            _quitInProgress = true;
            var flushResult = await GameShutdownEvent.FireGameShutdownAsync(reason);
            if (flushResult == ShutdownFlushResult.FlushTimedOut)
                Debug.LogError("セーブの書き出し完了を待ち切れないままアプリを終了します");
            if (flushResult == ShutdownFlushResult.SaveAbandoned)
                Debug.LogError("セーブの書き出しを諦めたため、世界が保存されないままアプリを終了します");
            if (flushResult == ShutdownFlushResult.FlushFailed)
                Debug.LogError("終了時の書き出しが例外で失敗したため、何が保存されたか分からないままアプリを終了します");

            // 終了口へ届いたことを残す。届かないまま残留したときに、どこで止まったかを Player.log で切り分けられる
            // Record reaching the exit, so a lingering process can be bisected in Player.log
            Debug.Log($"書き出しを待ち終えたためアプリを終了します reason:{reason} flush:{flushResult}");
            AllowQuitAndExit();
        }

        private static void AllowQuitAndExit()
        {
            _quitAllowed = true;
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
