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

        [Key(5)] public float ScaleX { get; set; }
        [Key(6)] public float ScaleY { get; set; }
        [Key(7)] public float ScaleZ { get; set; }

        // 姿勢はクォータニオンの4成分。斜面法線への傾きとランダムYを配置器が持たせている
        // The rotation as the quaternion's four components; the placers give it the slope tilt and a random yaw
        [Key(8)] public float RotationX { get; set; }
        [Key(9)] public float RotationY { get; set; }
        [Key(10)] public float RotationZ { get; set; }
        [Key(11)] public float RotationW { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MapObjectLayoutMessagePack() { }

        // 引数順はKey昇順(X,Y,Z→Scale→Rotation)に揃えてある。読み違いを防ぐための整列
        // Argument order mirrors ascending Key order (X,Y,Z -> Scale -> Rotation), kept aligned to avoid misreading
        public MapObjectLayoutMessagePack(
            int instanceId, string mapObjectGuid, float x, float y, float z,
            float scaleX, float scaleY, float scaleZ,
            float rotationX, float rotationY, float rotationZ, float rotationW)
        {
            InstanceId = instanceId;
            MapObjectGuid = mapObjectGuid;
            X = x;
            Y = y;
            Z = z;
            ScaleX = scaleX;
            ScaleY = scaleY;
            ScaleZ = scaleZ;
            RotationX = rotationX;
            RotationY = rotationY;
            RotationZ = rotationZ;
            RotationW = rotationW;
        }
    }
}
