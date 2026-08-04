using Cysharp.Threading.Tasks;

namespace Client.Game.Common
{
    /// <summary>
    ///     初期イベント（ディスパッチ開始時にreplayされるsnapshot等）の適用完了を初期化パイプラインが待つ対象
    ///     A target the init pipeline waits on until its initial events (snapshots replayed on dispatch start) are applied
    /// </summary>
    public interface IInitialEventApplyWaitTarget
    {
        // DI登録された全対象の完了を初期化パイプラインが待つ。失敗は例外として待機境界へ届く
        // The init pipeline awaits every registered target; failures reach the waiting boundary as exceptions
        UniTask WaitForInitialApplyAsync();
    }
}
