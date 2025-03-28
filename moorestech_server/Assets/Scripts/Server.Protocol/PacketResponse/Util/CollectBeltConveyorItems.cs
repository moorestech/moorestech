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
                
                //残り時間をどこまで進んだかに変換するために 1- する
                var percent = 1f - (float)beltConveyorItem.RemainingPercent;
                
                _itemInstanceIdToPercent[beltConveyorItem.ItemInstanceId] = percent;
                
                float entityX = pos.x;
                float entityZ = pos.z;
                switch (blockDirection)
                {
                    case BlockDirection.North:
                        entityX += 0.5f; //ベルトコンベアの基準座標は中心なので0.5を他してアイテムを中心に持ってくる
                        entityZ += percent;
                        break;
                    case BlockDirection.South:
                        entityX += 0.5f;
                        entityZ += 1 - percent; //北とは逆向きなので1を引いて逆向きにする
                        break;
                    case BlockDirection.East:
                        entityX += percent;
                        entityZ += 0.5f;
                        break;
                    case BlockDirection.West:
                        entityX += 1 - percent;
                        entityZ += 0.5f;
                        break;
                }
                
                //この0.3という値は仮
                var y = pos.y + DefaultBeltConveyorHeight;
                
                if (beltConveyor.SlopeType == BeltConveyorSlopeType.Up)
                {
                    y += percent;
                    y += 0.1f;
                }
                else if (beltConveyor.SlopeType == BeltConveyorSlopeType.Down)
                {
                    y -= percent;
                    y += 0.1f;
                    y++;
                }
                
                var position = new Vector3(entityX, y, entityZ);
                var itemEntity = (ItemEntity)entityFactory.CreateEntity(VanillaEntityType.VanillaItem, new EntityInstanceId(beltConveyorItem.ItemInstanceId.AsPrimitive()), position);
                itemEntity.SetState(beltConveyorItem.ItemId, 1);
                
                result.Add(itemEntity);
            }
            
            return result;
        }
    }
}