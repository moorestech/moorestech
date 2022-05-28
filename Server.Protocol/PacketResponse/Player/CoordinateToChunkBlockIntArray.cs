using Game.World.Interface.DataStore;
using Game.WorldMap;
using Server.Protocol.PacketResponse.Const;

namespace Server.Protocol.PacketResponse.Player
{
    public static class CoordinateToChunkBlockIntArray
    {
        /// <summary>
        /// チャンクの原点からそこにあるブロックのID一覧を返す
        /// </summary>
        /// <param name="coordinate"></param>
        /// <param name="worldBlockDatastore"></param>
        /// <returns></returns>
        public static int[,] GetBlockIdsInChunk(Coordinate coordinate, IWorldBlockDatastore worldBlockDatastore)
        {
            //その座標のチャンクの原点
            var (x, y) = ChunkResponseConst.BlockPositionToChunkOriginPosition(coordinate.X, coordinate.Y);

            var blocks = new int[ChunkResponseConst.ChunkSize, ChunkResponseConst.ChunkSize];

            for (int i = 0; i < blocks.GetLength(0); i++)
            {
                for (int j = 0; j < blocks.GetLength(1); j++)
                {
                    blocks[i, j] = worldBlockDatastore.GetBlock(
                        x + i,
                        y + j).BlockId;
                }
            }

            return blocks;
        }

        public static BlockDirection[,] GetBlockDirectionInChunk(Coordinate coordinate,
            IWorldBlockDatastore worldBlockDatastore)
        {
            var (x, y) = ChunkResponseConst.BlockPositionToChunkOriginPosition(coordinate.X, coordinate.Y);
            
            var blockDirections = new BlockDirection[ChunkResponseConst.ChunkSize, ChunkResponseConst.ChunkSize];

            for (int i = 0; i < blockDirections.GetLength(0); i++)
            {
                for (int j = 0; j < blockDirections.GetLength(1); j++)
                {
                    blockDirections[i, j] = worldBlockDatastore.GetBlockDirection(
                        x + i,
                        y + j);
                }
            }

            return blockDirections;
            
        }
        
        /// <summary>
        /// チャンクの原点からマップタイルのID一覧を返す
        /// </summary>
        /// <param name="coordinate"></param>
        /// <param name="worldMapTile"></param>
        /// <returns></returns>
        public static int[,] GetMapIdsInChunk(Coordinate coordinate, WorldMapTile worldMapTile)
        {
            //その座標のチャンクの原点
            var (x, y) = ChunkResponseConst.BlockPositionToChunkOriginPosition(coordinate.X, coordinate.Y);

            var blocks = new int[ChunkResponseConst.ChunkSize, ChunkResponseConst.ChunkSize];

            for (int i = 0; i < blocks.GetLength(0); i++)
            {
                for (int j = 0; j < blocks.GetLength(1); j++)
                {
                    blocks[i, j] = worldMapTile.GetMapTile(
                        x + i,
                        y + j);
                }
            }

            return blocks;
        }
    }
}