using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Game.Common
{
    /// <summary>
    /// ゲームの終了パイプラインイベント
    /// Game shutdown pipeline event
    /// </summary>
    public static class GameShutdownEvent
    {
        private static readonly Subject<GameShutdownReason> _onGameShutdown = new();
        private static readonly Subject<ShutdownFlushResult> _onShutdownFlushed = new();
        private static readonly List<IGameShutdownParticipant> _participants = new();
        private static bool _fired;
        private static bool _quitDeferralInstalled;
        private static bool _quitInProgress;
        private static bool _quitAllowed;

        // 終了理由つきで発火するイベント。終了の意思が表明された時点で飛び、書き出しの完了は待たない
        // Event carrying the shutdown reason; it fires when the intent to exit is declared, without waiting for any flush
        public static IObservable<GameShutdownReason> OnGameShutdown => _onGameShutdown;

        // 全参加者の書き出しが終わった時点で、畳んだ結果つきで飛ぶ。意思表明（OnGameShutdown）と分けるのは、終了処理中の停止を検知可能にするため
        // Fires once every participant's flush has finished, carrying the folded result; kept apart from the intent (OnGameShutdown) so a stall during shutdown stays detectable
        public static IObservable<ShutdownFlushResult> OnShutdownFlushed => _onShutdownFlushed;

        // 起動シーケンスの開始でガードを戻す。初期化失敗が続いても各回の終了通知を落とさない
        // Reset the guard when a boot sequence starts, so repeated initialization failures never drop a shutdown
        public static void ResetForNewSession()
        {
            _fired = false;
            _participants.Clear();
        }

        // ウィンドウを閉じる等のOS由来の終了要求を一度止め、書き出しを待つ正規の終了口へ流す。止めないと完了前にプロセスが消える
        // Holds OS-originated quit requests (closing the window) and routes them through the awaiting exit; otherwise the process dies before the flush
        public static void InstallApplicationQuitDeferral()
        {
            // Editorの終了要求を止めるとEditor自体が閉じられなくなる。Editorでの停止は待たずに InstallUnannouncedExitNotice の保険が記録する
            // Holding the Editor's own quit would keep the Editor from closing; an Editor stop is instead recorded, without waiting, by InstallUnannouncedExitNotice
            if (Application.isEditor || _quitDeferralInstalled) return;
            _quitDeferralInstalled = true;
            Application.wantsToQuit += OnApplicationWantsToQuit;
        }

        // 終了の意思表明が誰からも出ないまま終わる経路の保険。シーンの生存に依らず初期化途中やゲート待ちも扱う
        // Catch undeclared exits independently of scene lifetime, including stops during initialization or start gates
        public static void InstallUnannouncedExitNotice()
        {
            // 起動シーケンスは再入する（Editorの再生し直し）。重複購読を機械的に防ぐ
            // The boot sequence re-enters (an Editor replay), so a duplicate subscription is ruled out mechanically
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting()
        {
            NotifyUnannouncedExit();
        }

        // 正規の終了口を通った終了には介入せず、終了処理中の停止の検知を保つ
        // Leave declared exits alone to preserve detection of stalls during their shutdown
        internal static bool NotifyUnannouncedExit()
        {
            if (_fired)
            {
                Debug.Log("終了の意思表明は既に通知済みのため、保険の終了通知は行いません");
                return false;
            }
            Debug.Log("終了の意思表明が無いまま終了要求が来たため、待てない終了として記録します（エディタのPlay停止・OS由来の終了）");
            FireGameShutdown(GameShutdownReason.UnawaitableExit);
            return true;
        }

        private static bool OnApplicationWantsToQuit()
        {
            if (_quitAllowed) return true;
            if (_quitInProgress)
            {
                Debug.Log("終了処理の書き出し中のため、重ねて来た終了要求は保留します");
                return false;
            }
            Debug.Log("終了要求を保留し、書き出しの完了を待ってから終了します");
            QuitApplicationAsync().Forget(LogShutdownFailure);
            return false;
        }

        // 終了時に書き出しを終わらせる相手を登録する。待ち上限は参加者自身が持つ
        // Register who must finish writing at shutdown; the time budget belongs to the participant
        public static void RegisterParticipant(IGameShutdownParticipant participant)
        {
            if (_participants.Contains(participant)) return;
            _participants.Add(participant);
        }

        public static void UnregisterParticipant(IGameShutdownParticipant participant)
        {
            _participants.Remove(participant);
        }

        // 待てない経路（メインメニューへの復帰・破棄）用の通知。書き出し待ちは観測付きで併走させる
        // Notification for paths that cannot await (returning to the menu, teardown); the flush runs alongside, observed
        public static void FireGameShutdown(GameShutdownReason reason)
        {
            if (_fired) return;
            FireGameShutdownAsync(reason).Forget(LogShutdownFailure);
        }

        // 発火して全参加者の書き出し完了まで待つ。待てる終了経路はこちらを通す
        // Fire and await every participant's flush; every awaitable exit path goes through here
        public static async UniTask<ShutdownFlushResult> FireGameShutdownAsync(GameShutdownReason reason)
        {
            // 同一セッション内の二重発火（Back → LoadScene → OnDestroy）を弾く
            // Suppress double-fire within the same session (Back → LoadScene → OnDestroy)
            if (_fired) return ShutdownFlushResult.AlreadyShutdown;
            _fired = true;
            _onGameShutdown.OnNext(reason);

            // 購読中に登録された分を取り切ってから待つ。参加者の再入を避けリストは先に空にする
            // Take what the subscribers just registered and clear first, avoiding participant re-entry
            var participants = _participants.ToArray();
            _participants.Clear();

            var flushTasks = new UniTask<ShutdownFlushResult>[participants.Length];
            for (var i = 0; i < participants.Length; i++) flushTasks[i] = FlushIsolated(participants[i]);
            var results = await UniTask.WhenAll(flushTasks);

            // 戻り値は1つだけなので、畳んで消える失敗は捨てる前にログへ残す
            // Only one value can come back, so the failures that folding erases are logged before they go
            var aggregated = ShutdownFlushResultAggregator.AggregateAndReport(results);
            _onShutdownFlushed.OnNext(aggregated);
            return aggregated;
        }

        // アプリを終了する唯一の口。書き出しを待ってから落とす
        // The single application-exit entry point; waits for the flush before going down
        public static async UniTask QuitApplicationAsync()
        {
            _quitInProgress = true;
            var flushResult = await FireGameShutdownAsync(GameShutdownReason.IntentionalExit);
            if (flushResult == ShutdownFlushResult.FlushTimedOut)
                Debug.LogError("セーブの書き出し完了を待ち切れないままアプリを終了します");
            if (flushResult == ShutdownFlushResult.SaveAbandoned)
                Debug.LogError("セーブの書き出しを諦めたため、世界が保存されないままアプリを終了します");
            if (flushResult == ShutdownFlushResult.FlushFailed)
                Debug.LogError("終了時の書き出しが例外で失敗したため、何が保存されたか分からないままアプリを終了します");

            _quitAllowed = true;
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // 参加者の書き出しはディスクという外部資源に触れる。1人の例外を隔離しないと、後続の参加者（ワールドのセーブ）が起動すらせず Application.Quit にも到達しない
        // A participant's flush touches the disk, an external resource; without isolation one exception keeps later participants (the world save) from even starting and strands Application.Quit
        private static async UniTask<ShutdownFlushResult> FlushIsolated(IGameShutdownParticipant participant)
        {
            try
            {
                return await participant.FlushOnShutdownAsync();
            }
            catch (Exception exception)
            {
                Debug.LogError($"終了時の書き出しが失敗しました（他の参加者の書き出しは続行します） {participant.GetType().Name}: {exception.GetBaseException().Message}");
                return ShutdownFlushResult.FlushFailed;
            }
        }

        private static void LogShutdownFailure(Exception exception)
        {
            Debug.LogError($"終了処理が失敗しました: {exception.GetType()} {exception.Message}\n{exception.StackTrace}");
        }
    }
}
