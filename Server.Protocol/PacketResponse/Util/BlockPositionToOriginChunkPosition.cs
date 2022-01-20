using Server.Protocol.PacketResponse.Const;

namespace Server.Protocol.PacketResponse.Util
{
    public class BlockPositionToOriginChunkPosition
    {
        public (int,int) Convert(int x, int y)
        {
            return (GetChunk(x),GetChunk(y));
        }

        private int GetChunk(int n)
        {
            int chunk = n / ChunkResponseConst.ChunkSize;
            
            
            if (n < 0 && n % ChunkResponseConst.ChunkSize != 0)
            {
                chunk--;
            }
            int chunkPosition = chunk * ChunkResponseConst.ChunkSize;
            return chunkPosition;
        }
        
    }
}