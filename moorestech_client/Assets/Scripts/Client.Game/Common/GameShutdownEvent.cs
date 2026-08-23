using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.Game.Common
{
    /// <summary>
    /// ゲームの終了パイプラインイベント
    /// Game shutdown pipeline event
    /// </summary>
    public static class GameShutdownEvent
    {
        private static readonly Subject<Unit> _onGameShutdown = new();
        private static readonly List<UniTask> _shutdownTasks = new();
        private static bool _fired;

        // ゲーム終了時に発火するイベント
        // Event fired when game shutdown begins
        public static IObservable<Unit> OnGameShutdown => _onGameShutdown;

        // 起動シーケンスの開始でガードを戻す。初期化失敗が続いても各回の終了通知を落とさない
        // Reset the guard when a boot sequence starts, so repeated initialization failures never drop a shutdown
        public static void ResetForNewSession()
        {
            _fired = false;
            _shutdownTasks.Clear();
        }

        // 終了通知を受けた側が「完了まで待たせたい処理」を預ける。待ち上限は預ける側が持つ
        // Subscribers hand over the work the shutdown must wait for; the time budget belongs to the registrant
        public static void RegisterShutdownTask(UniTask shutdownTask)
        {
            _shutdownTasks.Add(shutdownTask);
        }

        public static void FireGameShutdown()
        {
            // 同一セッション内の二重発火（Back → LoadScene → OnDestroy）を弾く
            // Suppress double-fire within the same session (Back → LoadScene → OnDestroy)
            if (_fired) return;
            _fired = true;
            _onGameShutdown.OnNext(Unit.Default);
        }

        // 発火して登録された終了処理の完了まで待つ。アプリ終了経路はこちらを通す
        // Fire and await every registered shutdown task; the app-exit path goes through here
        public static async UniTask FireGameShutdownAsync()
        {
            FireGameShutdown();

            // 購読中に積まれた分を取り切ってから待つ。UniTaskは二度awaitできないためリストは先に空にする
            // Take what the subscribers just queued and clear first, since a UniTask cannot be awaited twice
            var pendingTasks = _shutdownTasks.ToArray();
            _shutdownTasks.Clear();
            await UniTask.WhenAll(pendingTasks);
        }
    }
}
