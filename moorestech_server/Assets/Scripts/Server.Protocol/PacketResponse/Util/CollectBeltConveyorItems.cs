using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Entity.Interface;
using Game.Entity.Interface.EntityInstance;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlockConnectInfoModule;
using UnityEngine;


namespace Server.Protocol.PacketResponse.Util
{
    /// <summary>
    ///     ベルトコンベアにあるアイテムを収集し、エンティティに変換して返す
    /// </summary>
    public static class CollectBeltConveyorItems
    {
        public const float DefaultBeltConveyorHeight = 0.3f;
        
        public static List<IEntity> CollectItem(IEntityFactory entityFactory)
        {
            var result = new List<IEntity>();
            result.AddRange(CollectItemFromWorld(entityFactory));
            
            return result;
        }
        
        public static List<IEntity> CollectItemFromWorld(IEntityFactory entityFactory)
        {
            var result = new List<IEntity>();
            
            //TODO 個々のパフォーマンス問題を何とかする
            foreach (KeyValuePair<BlockInstanceId, WorldBlockData> blockMaster in ServerContext.WorldBlockDatastore.BlockMasterDictionary)
            {
                var block = blockMaster.Value.Block;
                var pos = blockMaster.Value.BlockPositionInfo.OriginalPos;
                // TODO 重かったら考える
                if (!block.TryGetComponent<IItemCollectableBeltConveyor>(out var component))
                {
                    continue;
                }
                
                var direction = ServerContext.WorldBlockDatastore.GetBlockDirection(pos);
                result.AddRange(CollectItemFromBeltConveyor(entityFactory, component, pos, direction));
            }
            
            return result;
        }
        
        static Dictionary<ItemInstanceId,float> _itemInstanceIdToPercent = new Dictionary<ItemInstanceId, float>();
        
        private static List<IEntity> CollectItemFromBeltConveyor(IEntityFactory entityFactory, IItemCollectableBeltConveyor beltConveyor, Vector3Int pos, BlockDirection blockDirection)
        {
            var result = new List<IEntity>();
            for (var i = 0; i < beltConveyor.BeltConveyorItems.Count; i++)
            {
                var beltConveyorItem = beltConveyor.BeltConveyorItems[i];
                if (beltConveyorItem == null) continue;
                if (beltConveyorItem.ItemId == ItemMaster.EmptyItemId) continue;
                
                // 残りtickから進捗割合を計算する
                // Calculate progress ratio from remaining ticks
                var progressPercent = beltConveyorItem.TotalTicks > 0
                    ? 1f - (float)beltConveyorItem.RemainingTicks / beltConveyorItem.TotalTicks
                    : 0f;
                
                _itemInstanceIdToPercent[beltConveyorItem.ItemInstanceId] = progressPercent;
                
                float entityX = pos.x;
                float entityZ = pos.z;
                switch (blockDirection)
                {
                    case BlockDirection.North:
                        entityX += 0.5f; //ベルトコンベアの基準座標は中心なので0.5を他してアイテムを中心に持ってくる
                        entityZ += progressPercent;
                        break;
                    case BlockDirection.South:
                        entityX += 0.5f;
                        entityZ += 1 - progressPercent; //北とは逆向きなので1を引いて逆向きにする
                        break;
                    case BlockDirection.East:
                        entityX += progressPercent;
                        entityZ += 0.5f;
                        break;
                    case BlockDirection.West:
                        entityX += 1 - progressPercent;
                        entityZ += 0.5f;
                        break;
                }
                
                //この0.3という値は仮
                var y = pos.y + DefaultBeltConveyorHeight;
                
                if (beltConveyor.SlopeType == BeltConveyorSlopeType.Up)
                {
                    y += progressPercent;
                    y += 0.1f;
                }
                else if (beltConveyor.SlopeType == BeltConveyorSlopeType.Down)
                {
                    y -= progressPercent;
                    y += 0.1f;
                    y++;
                }
                
                var position = new Vector3(entityX, y, entityZ);
                var itemEntity = (BeltConveyorItemEntity)entityFactory.CreateEntity(VanillaEntityType.VanillaItem, new EntityInstanceId(beltConveyorItem.ItemInstanceId.AsPrimitive()), position);

                // ConnectorからGuidを直接取得
                // Get Guid directly from Connector
                var sourceConnectorGuid = GetConnectorGuidFromConnector(beltConveyorItem.StartConnector);
                var goalConnectorGuid = GetConnectorGuidFromConnector(beltConveyorItem.GoalConnector);
                itemEntity.SetItemData(beltConveyorItem.ItemId, 1, sourceConnectorGuid, goalConnectorGuid, progressPercent, pos);

                result.Add(itemEntity);
            }
            
            return result;
        }

        /// <summary>
        /// BlockConnectInfoElementからConnectorGuidを取得
        /// Get ConnectorGuid from BlockConnectInfoElement
        /// </summary>
        private static Guid? GetConnectorGuidFromConnector(BlockConnectInfoElement connector)
        {
            return connector?.ConnectorGuid;
        }
    }
}
