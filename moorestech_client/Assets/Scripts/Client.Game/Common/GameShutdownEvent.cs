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
        private static readonly Subject<Unit> _onGameShutdown = new();
        private static readonly List<IGameShutdownParticipant> _participants = new();
        private static bool _fired;

        // ゲーム終了時に発火するイベント
        // Event fired when game shutdown begins
        public static IObservable<Unit> OnGameShutdown => _onGameShutdown;

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
        public static void FireGameShutdown()
        {
            if (_fired) return;
            FireGameShutdownAsync().Forget(LogShutdownFailure);
        }

        // 発火して全参加者の書き出し完了まで待つ。待てる終了経路はこちらを通す
        // Fire and await every participant's flush; every awaitable exit path goes through here
        public static async UniTask<ShutdownFlushResult> FireGameShutdownAsync()
        {
            // 同一セッション内の二重発火（Back → LoadScene → OnDestroy）を弾く
            // Suppress double-fire within the same session (Back → LoadScene → OnDestroy)
            if (_fired) return ShutdownFlushResult.AlreadyShutdown;
            _fired = true;
            _onGameShutdown.OnNext(Unit.Default);

            // 購読中に登録された分を取り切ってから待つ。参加者の再入を避けリストは先に空にする
            // Take what the subscribers just registered and clear first, avoiding participant re-entry
            var participants = _participants.ToArray();
            _participants.Clear();

            var flushTasks = new UniTask<ShutdownFlushResult>[participants.Length];
            for (var i = 0; i < participants.Length; i++) flushTasks[i] = participants[i].FlushOnShutdownAsync();
            var results = await UniTask.WhenAll(flushTasks);

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
            var flushResult = await FireGameShutdownAsync();
            if (flushResult == ShutdownFlushResult.FlushTimedOut)
                Debug.LogError("セーブの書き出し完了を待ち切れないままアプリを終了します");

            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private static void LogShutdownFailure(Exception exception)
        {
            Debug.LogError($"終了処理が失敗しました: {exception.GetType()} {exception.Message}\n{exception.StackTrace}");
        }
    }
}
