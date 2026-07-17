namespace Game.Context
{
    /// <summary>
    ///     初期ロード完了後にLoadが一括で呼ばれるサービスのマーカー。ロード中のイベントをクライアントへ配信しないため購読等の初期化はLoadで行う。生成保証は基底のIAutoInstantiatedが表す
    ///     Marker for services whose Load is invoked in bulk after initial world load; subscribe in Load so load-time events are not broadcast to clients. The creation guarantee comes from IAutoInstantiated
    /// </summary>
    public interface IPostLoadInitializable : IAutoInstantiated
    {
        void Load();
    }
}
