namespace Game.Context
{
    /// <summary>
    ///     初期ロード完了後にLoadが一括で呼ばれるサービスのマーカー。ロード中のイベントをクライアントへ配信しない用途。生成保証とLoad契約は基底のIAutoInstantiatedが持つ
    ///     Marker for services whose Load is invoked in bulk after initial world load, so load-time events are not broadcast to clients. The creation guarantee and Load contract come from IAutoInstantiated
    /// </summary>
    public interface IPostLoadInitializable : IAutoInstantiated
    {
    }
}
