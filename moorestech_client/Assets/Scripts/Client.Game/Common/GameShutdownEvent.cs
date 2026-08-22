using System;
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
        private static bool _fired;

        // ゲーム終了時に発火するイベント
        // Event fired when game shutdown begins
        public static IObservable<Unit> OnGameShutdown => _onGameShutdown;

        // 起動シーケンスの開始でガードを戻す。初期化失敗が続いても各回の終了通知を落とさない
        // Reset the guard when a boot sequence starts, so repeated initialization failures never drop a shutdown
        public static void ResetForNewSession()
        {
            _fired = false;
        }

        public static void FireGameShutdown()
        {
            // 同一セッション内の二重発火（Back → LoadScene → OnDestroy）を弾く
            // Suppress double-fire within the same session (Back → LoadScene → OnDestroy)
            if (_fired) return;
            _fired = true;
            _onGameShutdown.OnNext(Unit.Default);
        }
    }
}
