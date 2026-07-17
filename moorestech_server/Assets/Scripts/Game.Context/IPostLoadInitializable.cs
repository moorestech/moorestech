namespace Game.Context
{
    /// <summary>
    ///     サーバー起動時にDIコンテナから自動で一括生成され、初期ロード完了後にLoadが一括で呼ばれるサービスのマーカー。ロード中のイベントをクライアントへ配信しないため購読等の初期化はLoadで行う
    ///     Marker for services auto-created in bulk at server boot whose Load is invoked in bulk after initial world load; subscribe in Load so load-time events are not broadcast to clients
    /// </summary>
    public interface IPostLoadInitializable
    {
        void Load();
    }
}
