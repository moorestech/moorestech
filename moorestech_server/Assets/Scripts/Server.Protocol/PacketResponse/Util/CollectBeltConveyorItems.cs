using System.Collections.Generic;
using Game.Block;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface.BlockConfig;
using Game.Entity.Interface;
using Game.Entity.Interface.EntityInstance;
using Game.World.Interface.DataStore;
using Server.Protocol.PacketResponse.Const;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util
{
    /// <summary>
    ///     ベルトコンベアにあるアイテムを収集し、エンティティに変換して返す
    /// </summary>
    public static class CollectBeltConveyorItems
    {
        public static List<IEntity> CollectItem(List<Vector2Int> collectChunks,
            IWorldBlockDatastore worldBlockDatastore, IBlockConfig blockConfig, IEntityFactory entityFactory)
        {
            var result = new List<IEntity>();
            foreach (var collectChunk in collectChunks)
                result.AddRange(CollectItemFromChunk(collectChunk, worldBlockDatastore, blockConfig, entityFactory));

            return result;
        }


        private static List<IEntity> CollectItemFromChunk(Vector2Int chunk,
            IWorldBlockDatastore worldBlockDatastore, IBlockConfig blockConfig, IEntityFactory entityFactory)
        {
            var result = new List<IEntity>();
            for (var i = 0; i < ChunkResponseConst.ChunkSize; i++)
            for (var j = 0; j < ChunkResponseConst.ChunkSize; j++)
            {
                var x = i + chunk.x;
                var y = j + chunk.y;

                if (!worldBlockDatastore.TryGetBlock(x, y, out var block)) continue;

                var type = blockConfig.GetBlockConfig(block.BlockId).Type;

                if (type != VanillaBlockType.BeltConveyor) continue;

                var direction = worldBlockDatastore.GetBlockDirection(x, y);

                result.AddRange(CollectItemFromBeltConveyor(entityFactory, (VanillaBeltConveyor)block, x, y,
                    direction));
            }

            return result;
        }


        private static List<IEntity> CollectItemFromBeltConveyor(IEntityFactory entityFactory,
            VanillaBeltConveyor vanillaBeltConveyor, int x, int y, BlockDirection blockDirection)
        {
            var result = new List<IEntity>();
            for (var i = 0 ; i < vanillaBeltConveyor.InventoryItemNum; i++)
            {
                var beltConveyorItem = vanillaBeltConveyor.GetInventoryItem(i);
                //残り時間をどこまで進んだかに変換するために 1- する
                var parcent =
                    1 - (float)(beltConveyorItem.RemainingTime / vanillaBeltConveyor.TimeOfItemEnterToExit);
                float entityX = x;
                float entityY = y;
                switch (blockDirection)
                {
                    case BlockDirection.North:
                        entityX += 0.5f; //ベルトコンベアの基準座標は中心なので0.5を他してアイテムを中心に持ってくる
                        entityY += parcent;
                        break;
                    case BlockDirection.South:
                        entityX += 0.5f;
                        entityY += 1 - parcent; //北とは逆向きなので1を引いて逆向きにする
                        break;
                    case BlockDirection.East:
                        entityX += parcent;
                        entityY += 0.5f;
                        break;
                    case BlockDirection.West:
                        entityX += 1 - parcent;
                        entityY += 0.5f;
                        break;
                }

                //Unity側ではZ軸がサーバーのY軸になるため変換する
                var position = new Vector3(entityX, 0, entityY);

                var itemEntity = (ItemEntity)entityFactory.CreateEntity(VanillaEntityType.VanillaItem,
                    beltConveyorItem.ItemInstanceId, position);
                itemEntity.SetState(beltConveyorItem.ItemId, 1);

                result.Add(itemEntity);
            }

            return result;
        }
    }
}