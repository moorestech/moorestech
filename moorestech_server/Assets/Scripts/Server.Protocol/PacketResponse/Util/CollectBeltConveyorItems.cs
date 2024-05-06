using System.Collections.Generic;
using Game.Block;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Context;
using Game.Entity.Interface;
using Game.Entity.Interface.EntityInstance;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util
{
    /// <summary>
    ///     ベルトコンベアにあるアイテムを収集し、エンティティに変換して返す
    /// </summary>
    public static class CollectBeltConveyorItems
    {
        public static List<IEntity> CollectItem(List<Vector2Int> collectChunks, IEntityFactory entityFactory)
        {
            var result = new List<IEntity>();
            foreach (var collectChunk in collectChunks)
                result.AddRange(CollectItemFromChunk(entityFactory));

            return result;
        }


        public static List<IEntity> CollectItemFromChunk(IEntityFactory entityFactory)
        {
            var result = new List<IEntity>();

            //TODO 個々のパフォーマンス問題を何とかする
            foreach (var blockMaster in ServerContext.WorldBlockDatastore.BlockMasterDictionary)
            {
                var block = blockMaster.Value.Block;
                var pos = blockMaster.Value.BlockPositionInfo.OriginalPos;

                var type = ServerContext.BlockConfig.GetBlockConfig(block.BlockId).Type;

                if (type != VanillaBlockType.BeltConveyor) continue;

                var direction = ServerContext.WorldBlockDatastore.GetBlockDirection(pos);

                result.AddRange(CollectItemFromBeltConveyor(entityFactory, block.ComponentManager.GetComponent<VanillaBeltConveyorComponent>(), pos, direction));
            }

            return result;
        }


        private static List<IEntity> CollectItemFromBeltConveyor(IEntityFactory entityFactory, VanillaBeltConveyorComponent vanillaBeltConveyorComponent, Vector3Int pos, BlockDirection blockDirection)
        {
            var result = new List<IEntity>();
            for (var i = 0; i < vanillaBeltConveyorComponent.InventoryItemNum; i++)
            {
                var beltConveyorItem = vanillaBeltConveyorComponent.GetBeltConveyorItem(i);
                if (beltConveyorItem == null)
                {
                    continue;
                }

                //残り時間をどこまで進んだかに変換するために 1- する
                var percent = 1 - (float)(beltConveyorItem.RemainingTime / vanillaBeltConveyorComponent.TimeOfItemEnterToExit);
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
                if (block.Block.BlockConfigData.Name == VanillaBeltConveyorTemplate.SlopeUpBeltConveyor)
                {
                    y += percent;
                    y += 0.1f;
                }
                else if (block.Block.BlockConfigData.Name == VanillaBeltConveyorTemplate.SlopeDownBeltConveyor)
                {
                    y -= percent;
                    y += 0.1f;
                    y++;
                }

                var position = new Vector3(entityX, y, entityZ);
                var itemEntity = (ItemEntity)entityFactory.CreateEntity(VanillaEntityType.VanillaItem, beltConveyorItem.ItemInstanceId, position);
                itemEntity.SetState(beltConveyorItem.ItemId, 1);

                result.Add(itemEntity);
            }

            return result;
        }
    }
}