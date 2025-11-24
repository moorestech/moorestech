using Core.Master;

namespace Game.Context
{
    public interface IChainInventoryService
    {
        bool TryConsumeChainItem(int playerId, ItemId chainItemId);
    }
}
