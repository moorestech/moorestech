using System.Collections.Generic;
using Core.Master;
using Game.Block;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Factory.BlockTemplate;
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
                
                var type = BlockMaster.GetBlockMaster(block.BlockId).BlockType;
                
                if (type != VanillaBlockType.BeltConveyor) continue;
                
                var direction = ServerContext.WorldBlockDatastore.GetBlockDirection(pos);
                var component = block.GetComponent<IItemCollectableBeltConveyor>();
                
                result.AddRange(CollectItemFromBeltConveyor(entityFactory, component, pos, direction));
            }
            
            return result;
        }
        
        private static List<IEntity> CollectItemFromBeltConveyor(IEntityFactory entityFactory, IItemCollectableBeltConveyor vanillaBeltConveyorComponent, Vector3Int pos, BlockDirection blockDirection)
        {
            var result = new List<IEntity>();
            for (var i = 0; i < vanillaBeltConveyorComponent.BeltConveyorItems.Count; i++)
            {
                var beltConveyorItem = vanillaBeltConveyorComponent.BeltConveyorItems[i];
                if (beltConveyorItem == null) continue;
                
                //残り時間をどこまで進んだかに変換するために 1- する
                var percent = 1f - (float)beltConveyorItem.RemainingPercent;
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
                var y = pos.y + VanillaBeltConveyorComponent.DefaultBeltConveyorHeight;
                
                var block = ServerContext.WorldBlockDatastore.GetOriginPosBlock(pos);
                if (block.Block.BlockElement.Name == VanillaBeltConveyorTemplate.SlopeUpBeltConveyor)
                {
                    y += percent;
                    y += 0.1f;
                }
                else if (block.Block.BlockElement.Name == VanillaBeltConveyorTemplate.SlopeDownBeltConveyor)
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