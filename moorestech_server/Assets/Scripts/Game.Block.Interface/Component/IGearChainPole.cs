using System.Collections.Generic;
using Core.Master;

namespace Game.Block.Interface.Component
{
    public interface IGearChainPole : IBlockComponent
    {
        BlockInstanceId BlockInstanceId { get; }
        float MaxConnectionDistance { get; }
        bool IsConnectionFull { get; }
        bool ContainsChainConnection(BlockInstanceId partnerId);
        bool TryAddChainConnection(BlockInstanceId partnerId, GearChainConnectionCost connectionCost);
        GearChainConnectionCost GetConnectionCost(BlockInstanceId partnerId);
    }
}
