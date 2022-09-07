using System.Collections.Generic;
using Core.Block.Blocks.BeltConveyor;
using Core.Block.Config;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using Server.Protocol.PacketResponse.Const;

namespace Server.Protocol.PacketResponse.Util
{
    public static class CollectBeltConveyorItems
    {
        public static List<IEntity> CollectItem(List<Coordinate> collectChunks,IWorldBlockDatastore worldBlockDatastore,IBlockConfig blockConfig,IEntityFactory entityFactory)
        {
            var result = new List<IEntity>();
            foreach (var collectChunk in collectChunks)
            {
                result.AddRange(CollectItemFromChunk(collectChunk,worldBlockDatastore,blockConfig,entityFactory));
            }

            return result;
        }


        private static List<IEntity> CollectItemFromChunk(Coordinate chunk, IWorldBlockDatastore worldBlockDatastore,IBlockConfig blockConfig,IEntityFactory entityFactory)
        {
            var result = new List<IEntity>();
            for (int i = 0; i < ChunkResponseConst.ChunkSize; i++)
            {
                for (int j = 0; j < ChunkResponseConst.ChunkSize; j++)
                {
                    var x = i + chunk.X;
                    var y = j + chunk.Y;
                    
                    if (!worldBlockDatastore.TryGetBlock(x,y,out var block))
                    {
                        continue;
                    }

                    var type = blockConfig.GetBlockConfig(block.BlockId).Type;

                    if (type != VanillaBlockType.BeltConveyor)
                    {
                        continue;
                    }

                    var direction = worldBlockDatastore.GetBlockDirection(x, y);
                    
                    result.AddRange(CollectItemFromBeltConveyor(entityFactory,(VanillaBeltConveyor)block,x,y,direction));
                }
            }

            return result;
        }


        private static List<IEntity> CollectItemFromBeltConveyor(IEntityFactory entityFactory,VanillaBeltConveyor vanillaBeltConveyor,int x,int y,BlockDirection blockDirection)
        {
            var result = new List<IEntity>();
            foreach (var beltConveyorItem in vanillaBeltConveyor.InventoryItems)
            { 
                //残り時間をどこまで進んだかに変換するために 1- する
                var parent = 1 - (float)(beltConveyorItem.RemainingTime / vanillaBeltConveyor.TimeOfItemEnterToExit);
                float entityX = x;
                float entityY = y;
                switch (blockDirection)
                {
                    case BlockDirection.North:
                        entityX += 0.5f; //ベルトコンベアの基準座標は中心なので0.5を他してアイテムを中心に持ってくる
                        entityY += parent;
                        break;
                    case BlockDirection.South:
                        entityX += 0.5f;
                        entityY -= parent;
                        break;
                    case BlockDirection.East:
                        entityX += parent;
                        entityY += 0.5f;
                        break;
                    case BlockDirection.West:
                        entityX -= parent;
                        entityY += 0.5f;
                        break;
                }
                
                //Unity側ではZ軸がサーバーのY軸になるため変換する
                var position = new ServerVector3(entityX,0,entityY);
                result.Add(entityFactory.CreateEntity(EntityType.VanillaItem,beltConveyorItem.ItemInstanceId,position));
            }

            return result;
        }
    }
}