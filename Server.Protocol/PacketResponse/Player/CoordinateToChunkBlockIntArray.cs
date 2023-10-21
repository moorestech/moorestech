using Game.World.Interface.DataStore;
using Game.WorldMap;
using Server.Protocol.PacketResponse.Const;

namespace Server.Protocol.PacketResponse.Player
{
    public static class CoordinateToChunkBlockIntArray
    {

        ///     ID

        /// <param name="coordinate"></param>
        /// <param name="worldBlockDatastore"></param>
        /// <returns></returns>
        public static int[,] GetBlockIdsInChunk(Coordinate coordinate, IWorldBlockDatastore worldBlockDatastore)
        {
            
            var (x, y) = ChunkResponseConst.BlockPositionToChunkOriginPosition(coordinate.X, coordinate.Y);

            var blocks = new int[ChunkResponseConst.ChunkSize, ChunkResponseConst.ChunkSize];

            for (var i = 0; i < blocks.GetLength(0); i++)
            for (var j = 0; j < blocks.GetLength(1); j++)
                blocks[i, j] = worldBlockDatastore.GetBlock(
                    x + i,
                    y + j).BlockId;

            return blocks;
        }

        public static int[,] GetBlockDirectionInChunk(Coordinate coordinate,
            IWorldBlockDatastore worldBlockDatastore)
        {
            var (x, y) = ChunkResponseConst.BlockPositionToChunkOriginPosition(coordinate.X, coordinate.Y);

            var blockDirections = new int[ChunkResponseConst.ChunkSize, ChunkResponseConst.ChunkSize];

            for (var i = 0; i < blockDirections.GetLength(0); i++)
            for (var j = 0; j < blockDirections.GetLength(1); j++)
                blockDirections[i, j] = (int)worldBlockDatastore.GetBlockDirection(
                    x + i,
                    y + j);

            return blockDirections;
        }


        ///     ID

        /// <param name="coordinate"></param>
        /// <param name="worldMapTile"></param>
        /// <returns></returns>
        public static int[,] GetMapIdsInChunk(Coordinate coordinate, WorldMapTile worldMapTile)
        {
            
            var (x, y) = ChunkResponseConst.BlockPositionToChunkOriginPosition(coordinate.X, coordinate.Y);

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