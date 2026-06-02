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
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        
        public OpenableInventoryResolver(IPlayerInventoryDataStore playerInventoryDataStore, ITrainUnitLookupDatastore trainUnitLookupDatastore)
        {
            _playerInventoryDataStore = playerInventoryDataStore;
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
            _resolvers = new Dictionary<InventoryType, IInventoryIdentifierResolver>();

            // 外部対象インベントリは専用リゾルバへ委譲する
            // Delegate external target inventories to dedicated resolvers.
            AddResolver(new BlockInventoryIdentifierResolver(ServerContext.WorldBlockDatastore));
            AddResolver(new TrainInventoryIdentifierResolver(_trainUnitLookupDatastore));
        }
        
        
        public static IOpenableInventory Resolve(
            InventoryIdentifierMessagePack inventoryIdentifier,
            IPlayerInventoryDataStore playerInventoryDataStore,
            ITrainUnitLookupDatastore trainUnitLookupDatastore)
        {
            var resolver = new OpenableInventoryResolver(playerInventoryDataStore, trainUnitLookupDatastore);
            return resolver.Resolve(inventoryIdentifier);
        }

        public IOpenableInventory Resolve(InventoryIdentifierMessagePack inventoryIdentifier)
        {
            if (inventoryIdentifier == null) return null;

            // プレイヤーインベントリはPlayerIdで直接解決する
            // Resolve player inventories directly by player id.
            return inventoryIdentifier.InventoryType switch
            {
                InventoryType.Main => _playerInventoryDataStore.GetInventoryData(inventoryIdentifier.PlayerId).MainOpenableInventory,
                InventoryType.Grab => _playerInventoryDataStore.GetInventoryData(inventoryIdentifier.PlayerId).GrabInventory,
                _ => ResolveByIdentifier(inventoryIdentifier),
            };

            #region Internal

            IOpenableInventory ResolveByIdentifier(InventoryIdentifierMessagePack identifier)
            {
                // InventoryTypeに対応するリゾルバへ委譲する
                // Delegate to the resolver registered for the inventory type.
                return _resolvers.TryGetValue(identifier.InventoryType, out var resolver)
                    ? resolver.Resolve(identifier)
                    : null;
            }

            #endregion
        }

        private void AddResolver(IInventoryIdentifierResolver resolver)
        {
            _resolvers.Add(resolver.InventoryType, resolver);
        }
    }
}
