namespace Server.Protocol.PacketResponse.Const
{
    public static class ChunkResponseConst
    {
        public const int ChunkSize = 20;
        public const int PlayerVisibleRangeChunk = 5;
        
        
        public static (int,int) BlockPositionToChunkOriginPosition(int x, int y)
        {
            return (GetChunk(x),GetChunk(y));
        }

        private static int GetChunk(int n)
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