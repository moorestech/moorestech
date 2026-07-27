using System;
using MessagePack;

namespace Server.Protocol.PacketResponse.MapData
{
    [MessagePackObject]
    public class MapObjectLayoutMessagePack
    {
        [Key(0)] public int InstanceId { get; set; }
        [Key(1)] public string MapObjectGuid { get; set; }
        [Key(2)] public float X { get; set; }
        [Key(3)] public float Y { get; set; }
        [Key(4)] public float Z { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MapObjectLayoutMessagePack() { }

        public MapObjectLayoutMessagePack(int instanceId, string mapObjectGuid, float x, float y, float z)
        {
            InstanceId = instanceId;
            MapObjectGuid = mapObjectGuid;
            X = x;
            Y = y;
            Z = z;
        }
    }
}
