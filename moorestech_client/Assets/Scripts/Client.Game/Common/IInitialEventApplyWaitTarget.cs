namespace Client.Game.Common
{
    /// <summary>
    ///     初期イベント（ディスパッチ開始時にreplayされるsnapshot等）の適用完了を初期化パイプラインが待つ対象
    ///     A target the init pipeline waits on until its initial events (snapshots replayed on dispatch start) are applied
    /// </summary>
    public interface IInitialEventApplyWaitTarget
    {
        // DI登録された全対象がtrueになるまで初期化パイプラインが待機する
        // The init pipeline waits until every registered target turns true
        bool IsInitialEventApplied { get; }
    }
}
