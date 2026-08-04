using System;
using MessagePack;

namespace Server.Protocol.PacketResponse.MapData
{
    [MessagePackObject]
    public class VeinLayoutMessagePack
    {
        [Key(0)] public string VeinGuid { get; set; }
        [Key(1)] public int MinX { get; set; }
        [Key(2)] public int MinY { get; set; }
        [Key(3)] public int MinZ { get; set; }
        [Key(4)] public int MaxX { get; set; }
        [Key(5)] public int MaxY { get; set; }
        [Key(6)] public int MaxZ { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public VeinLayoutMessagePack() { }

        public VeinLayoutMessagePack(string veinGuid, int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
        {
            VeinGuid = veinGuid;
            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
        }
    }
}
