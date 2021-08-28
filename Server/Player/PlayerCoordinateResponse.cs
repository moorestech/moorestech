using industrialization.Core.Block;
using industrialization.OverallManagement.DataStore;
using industrialization.Server.Const;

namespace industrialization.Server.Player
{
    public class PlayerCoordinateResponse
    {
        public Coordinate OriginPosition { get; }
        public int[,] Blocks { get; }
        public PlayerCoordinateResponse(Coordinate originPosition)
        {
            this.OriginPosition = originPosition;
            Blocks = new int[
                ChunkResponseConst.PlayerVisibleRangeChunk * ChunkResponseConst.ChunkSize,
                ChunkResponseConst.PlayerVisibleRangeChunk * ChunkResponseConst.ChunkSize];

            for (int i = 0; i < Blocks.GetLength(0); i++)
            {
                for (int j = 0; j < Blocks.GetLength(1); j++)
                {
                    Blocks[i, j] = BlockConst.NullIntId;
                }                
            }
        }
    }
}