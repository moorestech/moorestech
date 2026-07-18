namespace Game.Context
{
    /// <summary>
    ///     サーバー起動時にLoadが一括で呼ばれるサービスのマーカー。購読等の初期化はコンストラクタでなくLoadで行う。生成保証は基底のIAutoInstantiatedが表す
    ///     Marker for services whose Load is invoked in bulk at server boot. Initialization such as subscriptions belongs in Load, not the constructor. The creation guarantee comes from IAutoInstantiated
    /// </summary>
    public interface IBootInitializable : IAutoInstantiated
    {
        void Load();
    }
}
