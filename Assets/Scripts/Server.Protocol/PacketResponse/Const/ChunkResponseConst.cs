namespace Server.Protocol.PacketResponse.Const
{
    public static class ChunkResponseConst
    {
        public const int ChunkSize = 20;
        public const int PlayerVisibleRangeChunk = 5;


        public static (int, int) BlockPositionToChunkOriginPosition(int x, int y)
        {
            return (GetChunk(x), GetChunk(y));
        }

        private static int GetChunk(int n)
        {
            var chunk = n / ChunkSize;


            if (n < 0 && n % ChunkSize != 0) chunk--;
            var chunkPosition = chunk * ChunkSize;
            return chunkPosition;
        }
    }
}