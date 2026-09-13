using System;
using Client.Game.Common;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 終了パイプラインの発火を購読してマーカーを書く。終了処理側にプレイテストの語彙を持ち込まないための購読
    // Subscribes to the shutdown pipeline and writes the marker, keeping playtest vocabulary out of the shutdown code
    public sealed class CleanExitMarkWriter : IInitializable, IDisposable
    {
        private IDisposable _subscription;

        public void Initialize()
        {
            // 参加者の書き出し完了を待たずに、意図が表明された時点で書く（強制終了されても正常終了として残す）
            // Written the moment the intent is declared, without awaiting participant flushes, so a forced kill still counts as graceful
            _subscription = GameShutdownEvent.OnGameShutdown.Subscribe(_ => CleanExitMarker.MarkCleanExit());
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
