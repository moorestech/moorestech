using System.Collections.Generic;
using Core.Master;

namespace Game.Block.Interface.Component
{
    public interface IGearChainPole : IBlockComponent
    {
        BlockInstanceId BlockInstanceId { get; }
        float MaxConnectionDistance { get; }
        int MaxConnectionCount { get; }
        bool HasChainConnection { get; }
        bool IsConnectionFull { get; }
        IReadOnlyCollection<BlockInstanceId> PartnerIds { get; }
        bool ContainsChainConnection(BlockInstanceId partnerId);
        bool TryAddChainConnection(BlockInstanceId partnerId);
        bool RemoveChainConnection(BlockInstanceId partnerId);
        void ClearChainConnections();
    }
}
