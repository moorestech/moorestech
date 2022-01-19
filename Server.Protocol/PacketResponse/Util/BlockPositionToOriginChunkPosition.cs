using Server.Protocol.PacketResponse.Const;

namespace Server.Protocol.PacketResponse.Util
{
    public class BlockPositionToOriginChunkPosition
    {
        public (int,int) Convert(int x, int y)
        {
            int chunkX = x / ChunkResponseConst.ChunkSize;
	
            if (x < 0)
            {
                chunkX -= 1;
            }
            int chunkY = y / ChunkResponseConst.ChunkSize;
            if (y < 0)
            {
                chunkY -= 1;
            }
            int chunkXPosition = chunkX * ChunkResponseConst.ChunkSize;
            int chunkYPosition = chunkY * ChunkResponseConst.ChunkSize;
            
            return (chunkXPosition,chunkYPosition);
        }
    }
}