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
        private static readonly List<IGameShutdownParticipant> _participants = new();
        private static bool _fired;

        // 終了理由つきで発火するイベント
        // Event carrying the shutdown reason
        public static IObservable<GameShutdownReason> OnGameShutdown => _onGameShutdown;

        // 起動シーケンスの開始でガードを戻す。初期化失敗が続いても各回の終了通知を落とさない
        // Reset the guard when a boot sequence starts, so repeated initialization failures never drop a shutdown
        public static void ResetForNewSession()
        {
            _fired = false;
            _participants.Clear();
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

            // 諦めは上限到達より重い。世界が保存されていない事実は待ち切れなかった事実に埋もれてはいけない
            // A give-up outweighs a timeout: an unsaved world must not be hidden behind "did not finish waiting"
            foreach (var result in results)
                if (result == ShutdownFlushResult.SaveAbandoned)
                    return ShutdownFlushResult.SaveAbandoned;

            // 例外で落ちた参加者は「書けた」と名乗れない。正常値のNothingFlushedへ潰すと、保存されていない世界がFlushedとして閉じる
            // A participant that died on an exception cannot claim success; folding it into the normal NothingFlushed would close an unsaved world as Flushed
            foreach (var result in results)
                if (result == ShutdownFlushResult.FlushFailed)
                    return ShutdownFlushResult.FlushFailed;

            // 1つでも書き切れていなければ全体を上限到達として返す
            // Report the whole flush as timed out if any single participant failed to finish
            foreach (var result in results)
                if (result == ShutdownFlushResult.FlushTimedOut)
                    return ShutdownFlushResult.FlushTimedOut;
            return ShutdownFlushResult.Flushed;
        }

        // アプリを終了する唯一の口。書き出しを待ってから落とす
        // The single application-exit entry point; waits for the flush before going down
        public static async UniTask QuitApplicationAsync()
        {
            var flushResult = await FireGameShutdownAsync(GameShutdownReason.IntentionalExit);
            if (flushResult == ShutdownFlushResult.FlushTimedOut)
                Debug.LogError("セーブの書き出し完了を待ち切れないままアプリを終了します");
            if (flushResult == ShutdownFlushResult.SaveAbandoned)
                Debug.LogError("セーブの書き出しを諦めたため、世界が保存されないままアプリを終了します");
            if (flushResult == ShutdownFlushResult.FlushFailed)
                Debug.LogError("終了時の書き出しが例外で失敗したため、何が保存されたか分からないままアプリを終了します");

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
