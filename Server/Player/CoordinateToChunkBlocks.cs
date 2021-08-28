using industrialization.Core.Block;
using industrialization.OverallManagement.DataStore;
using industrialization.OverallManagement.Util;
using industrialization.Server.Const;

namespace industrialization.Server.Player
{
    public class CoordinateToChunkBlocks
    {
        public Coordinate OriginPosition { get; }
        public int[,] Blocks { get; }
        public CoordinateToChunkBlocks(Coordinate playerCoordinate)
        {
            //その座標のチャンクの原点
            var x = playerCoordinate.x / ChunkResponseConst.PlayerVisibleRangeChunk * ChunkResponseConst.PlayerVisibleRangeChunk;
            var y = playerCoordinate.x / ChunkResponseConst.PlayerVisibleRangeChunk * ChunkResponseConst.PlayerVisibleRangeChunk;
            OriginPosition = CoordinateCreator.New(x,y);
            
            Blocks = new int[ChunkResponseConst.ChunkSize,ChunkResponseConst.ChunkSize];

            for (int i = 0; i < Blocks.GetLength(0); i++)
            {
                for (int j = 0; j < Blocks.GetLength(1); j++)
                {
                    Blocks[i, j] = WorldBlockDatastore.GetBlock(
                        OriginPosition.x + i,
                        OriginPosition.y+ j).BlockId;
                }                
            }
        }
    }
}