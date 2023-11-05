using Core.Util;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using Server.Protocol.PacketResponse.Const;

namespace Server.Protocol.PacketResponse.Player
{
    public static class CoordinateToChunkBlockIntArray
    {
        /// <summary>
        ///     チャンクの原点からそこにあるブロックのID一覧を返す
        /// </summary>
        /// <param name="coreVector2Int"></param>
        /// <param name="worldBlockDatastore"></param>
        /// <returns></returns>
        public static int[,] GetBlockIdsInChunk(CoreVector2Int coreVector2Int, IWorldBlockDatastore worldBlockDatastore)
        {
            //その座標のチャンクの原点
            var (x, y) = ChunkResponseConst.BlockPositionToChunkOriginPosition(coreVector2Int.X, coreVector2Int.Y);

            var blocks = new int[ChunkResponseConst.ChunkSize, ChunkResponseConst.ChunkSize];

            for (var i = 0; i < blocks.GetLength(0); i++)
            for (var j = 0; j < blocks.GetLength(1); j++)
                blocks[i, j] = worldBlockDatastore.GetOriginPosBlock(
                    x + i,
                    y + j)?.Block.BlockId ?? 0;

            return blocks;
        }

        public static int[,] GetBlockDirectionInChunk(CoreVector2Int coreVector2Int,
            IWorldBlockDatastore worldBlockDatastore)
        {
            var (x, y) = ChunkResponseConst.BlockPositionToChunkOriginPosition(coreVector2Int.X, coreVector2Int.Y);

            var blockDirections = new int[ChunkResponseConst.ChunkSize, ChunkResponseConst.ChunkSize];

            for (var i = 0; i < blockDirections.GetLength(0); i++)
            for (var j = 0; j < blockDirections.GetLength(1); j++)
                blockDirections[i, j] = (int)(worldBlockDatastore.GetOriginPosBlock(
                    x + i,
                    y + j)?.BlockDirection ?? BlockDirection.North);

            return blockDirections;
        }

        /// <summary>
        ///     チャンクの原点からマップタイルのID一覧を返す
        /// </summary>
        /// <param name="coreVector2Int"></param>
        /// <param name="worldMapTile"></param>
        /// <returns></returns>
        public static int[,] GetMapIdsInChunk(CoreVector2Int coreVector2Int, WorldMapTile worldMapTile)
        {
            //その座標のチャンクの原点
            var (x, y) = ChunkResponseConst.BlockPositionToChunkOriginPosition(coreVector2Int.X, coreVector2Int.Y);

            var blocks = new int[ChunkResponseConst.ChunkSize, ChunkResponseConst.ChunkSize];

            for (var i = 0; i < blocks.GetLength(0); i++)
            for (var j = 0; j < blocks.GetLength(1); j++)
                blocks[i, j] = worldMapTile.GetMapTile(
                    x + i,
                    y + j);

            return blocks;
        }
    }
}