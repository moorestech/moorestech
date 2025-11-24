using Core.Master;

namespace Game.Block.Interface.Component
{
    public interface IChainPole : IBlockComponent
    {
        BlockInstanceId BlockInstanceId { get; }
        float MaxConnectionDistance { get; }
        bool HasChainConnection { get; }
        BlockInstanceId? PartnerId { get; }
        void SetChainConnection(BlockInstanceId partnerId);
        void ClearChainConnection();
    }
}
