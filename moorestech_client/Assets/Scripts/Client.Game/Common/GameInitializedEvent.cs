using System;
using UniRx;

namespace Client.Game.Common
{
    /// <summary>
    /// ゲームの初期化パイプラインイベント
    /// Game initialization pipeline event
    /// </summary>
    public static class GameInitializedEvent
    {
        private static readonly Subject<Unit> _onGameInitialized = new();

        // ゲーム初期化完了時に発火するイベント
        // Event fired when game initialization is complete
        public static IObservable<Unit> OnGameInitialized => _onGameInitialized;

        public static void FireGameInitialized()
        {
            _onGameInitialized.OnNext(Unit.Default);
        }
    }
}
