using Core.Inventory;
using Game.Block.Interface.Component;
using Game.World.Interface.DataStore;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.InventoryService.Resolver
{
    public class BlockInventoryIdentifierResolver : IInventoryIdentifierResolver
    {
        public InventoryType InventoryType => InventoryType.Block;

        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public BlockInventoryIdentifierResolver(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }

        public IOpenableInventory Resolve(InventoryIdentifierMessagePack identifier)
        {
            // ブロック座標から開けるインベントリコンポーネントを探す
            // Find the openable inventory component from the block position.
            var position = identifier.BlockPosition.Vector3Int;
            return _worldBlockDatastore.ExistsComponent<IOpenableBlockInventoryComponent>(position)
                ? _worldBlockDatastore.GetBlock<IOpenableBlockInventoryComponent>(position)
                : null;
        }
    }
}
