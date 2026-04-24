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

        static GameShutdownEvent()
        {
            // 新しいゲームセッション開始でガードをリセット。同一ドメインで連続プレイするケースに対応
            // Reset the guard when a new game session starts; handles consecutive plays without domain reload
            GameInitializedEvent.OnGameInitialized.Subscribe(_ => _fired = false);
        }

        // ゲーム終了時に発火するイベント
        // Event fired when game shutdown begins
        public static IObservable<Unit> OnGameShutdown => _onGameShutdown;

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
