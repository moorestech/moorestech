namespace Game.Block.Interface.Component.ConnectJudge
{
    /// <summary>
    ///     追加の接続条件を持たないドメイン用の判定（形状互換表のみで判定される）
    ///     Judge for domains without extra conditions (only the shape table applies)
    /// </summary>
    public class DefaultContext<TTarget> : IConnectorContext<TTarget> where TTarget : IBlockComponent
    {
        public void Initialize(IBlockConnectorComponent<TTarget> component, BlockPositionInfo positionInfo)
        {
           return; 
        }
        public bool CanConnect(ConnectJudgeContext context)
        {
            return true;
        }
    }
}
