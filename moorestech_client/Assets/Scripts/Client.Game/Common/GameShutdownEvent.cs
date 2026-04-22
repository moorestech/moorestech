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

        // ゲーム終了時に発火するイベント
        // Event fired when game shutdown begins
        public static IObservable<Unit> OnGameShutdown => _onGameShutdown;

        public static void FireGameShutdown()
        {
            _onGameShutdown.OnNext(Unit.Default);
        }
    }
}
