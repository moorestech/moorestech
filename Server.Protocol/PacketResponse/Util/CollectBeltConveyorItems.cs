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
                var parent = (float)(beltConveyorItem.RemainingTime / vanillaBeltConveyor.TimeOfItemEnterToExit);
                float entityX = x;
                float entityY = y;
                switch (blockDirection)
                {
                    case BlockDirection.North:
                        entityY += parent;
                        break;
                    case BlockDirection.South:
                        entityY -= parent;
                        break;
                    case BlockDirection.East:
                        entityX += parent;
                        break;
                    case BlockDirection.West:
                        entityX -= parent;
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