using System.Collections.Generic;
using Core.Inventory;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.Unit;
using Server.Protocol.PacketResponse.Util.InventoryService.Resolver;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.InventoryService
{
    /// <summary>
    /// アイテム移動・整理プロトコルで共通利用する、インベントリ種別から IOpenableInventory を解決するユーティリティ
    /// Utility shared by item move/sort protocols to resolve an IOpenableInventory from an inventory type
    /// </summary>
    public class OpenableInventoryResolver
    {
        private readonly Dictionary<InventoryType, IInventoryIdentifierResolver> _resolvers;

        public OpenableInventoryResolver(IPlayerInventoryDataStore playerInventoryDataStore, ITrainUnitLookupDatastore trainUnitLookupDatastore)
        {
            _resolvers = new Dictionary<InventoryType, IInventoryIdentifierResolver>();

            // すべてのインベントリ種別を専用リゾルバへ委譲する
            // Delegate every inventory type to a dedicated resolver.
            AddResolver(new MainInventoryIdentifierResolver(playerInventoryDataStore));
            AddResolver(new GrabInventoryIdentifierResolver(playerInventoryDataStore));
            AddResolver(new BlockInventoryIdentifierResolver(ServerContext.WorldBlockDatastore));
            AddResolver(new TrainInventoryIdentifierResolver(trainUnitLookupDatastore));

            #region Internal

            void AddResolver(IInventoryIdentifierResolver resolver)
            {
                _resolvers.Add(resolver.InventoryType, resolver);
            }

            #endregion
        }

        public IOpenableInventory Resolve(InventoryIdentifierMessagePack inventoryIdentifier)
        {
            if (inventoryIdentifier == null) return null;

            return _resolvers.TryGetValue(inventoryIdentifier.InventoryType, out var resolver)
                ? resolver.Resolve(inventoryIdentifier)
                : null;
        }
    }
}
