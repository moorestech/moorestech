namespace Game.Context
{
    /// <summary>
    ///     初期ロード完了後にDIコンテナから一括生成・初期化されるサービスのマーカー。ロード中のイベントをクライアントへ配信しないために初期化を遅らせる
    ///     Marker for services created and initialized in bulk after initial world load; initialization is deferred so load-time events are not broadcast to clients
    /// </summary>
    public interface IPostLoadInitializable
    {
    }
}
