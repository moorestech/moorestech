using System.Collections.Generic;
using Core.Inventory;
using Game.Block.Interface.Component;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Game.World.Interface.DataStore;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
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
        
        public OpenableInventoryResolver(IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _playerInventoryDataStore = playerInventoryDataStore;
            
            // WIP リゾルバ基盤 _resolvers.Add(InventoryType.Block, new BlockInventoryIdentifierResolver());
            // WIP _resolvers.Add(InventoryType.Train, new TrainInventoryIdentifierResolver());
        }
        
        
        public IOpenableInventory Resolve(
            ItemMoveInventoryType inventoryType,
            int playerId,
            InventoryIdentifierMessagePack inventoryIdentifier,
            IPlayerInventoryDataStore playerInventoryDataStore,
            ITrainUnitLookupDatastore trainUnitLookupDatastore)
        {
            return inventoryType switch
            {
                ItemMoveInventoryType.MainInventory => playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory,
                ItemMoveInventoryType.GrabInventory => playerInventoryDataStore.GetInventoryData(playerId).GrabInventory,
                ItemMoveInventoryType.SubInventory => ResolveSubInventory(inventoryIdentifier),
                _ => null,
            };

            #region Internal

            IOpenableInventory ResolveSubInventory(InventoryIdentifierMessagePack identifier)
            {
                // ブロック/列車インベントリの場合は InventoryIdentifier から情報を取得
                // Get information from InventoryIdentifier for block/train inventory.
                if (identifier == null) return null;

                return identifier.InventoryType switch
                {
                    InventoryType.Block => ResolveBlockInventory(identifier),
                    InventoryType.Train => ResolveTrainInventory(identifier),
                    _ => null,
                };
            }

            IOpenableInventory ResolveBlockInventory(InventoryIdentifierMessagePack identifier)
            {
                var pos = identifier.BlockPosition.Vector3Int;
                return ServerContext.WorldBlockDatastore.ExistsComponent<IOpenableBlockInventoryComponent>(pos)
                    ? ServerContext.WorldBlockDatastore.GetBlock<IOpenableBlockInventoryComponent>(pos)
                    : null;
            }

            IOpenableInventory ResolveTrainInventory(InventoryIdentifierMessagePack identifier)
            {
                // 列車カーのアイテムコンテナを IOpenableInventory として返す
                // Return the target train car item container as IOpenableInventory.
                var trainCarInstanceId = new TrainCarInstanceId(long.Parse(identifier.TrainCarInstanceId));
                if (!trainUnitLookupDatastore.TryGetTrainCar(trainCarInstanceId, out var trainCar)) return null;
                if (trainCar.Container is not ItemTrainCarContainer itemContainer) return null;
                return itemContainer;
            }

            #endregion
        }
    }
}
