namespace Game.Context
{
    /// <summary>
    ///     サーバー起動時にLoadが一括で呼ばれるサービスのマーカー。生成保証とLoad契約は基底のIAutoInstantiatedが持つ
    ///     Marker for services whose Load is invoked in bulk at server boot. The creation guarantee and Load contract come from IAutoInstantiated
    /// </summary>
    public interface IBootInitializable : IAutoInstantiated
    {
    }
}
