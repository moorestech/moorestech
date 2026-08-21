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

        // 岩クラスターの識別子と重心XZ。-1 は独立配置で、そのとき重心は (0,0) の未使用値
        // Rock cluster identifier plus its centroid XZ; -1 is an independent placement whose centroid stays an unused (0,0)
        [Key(8)] public int ClusterId { get; set; }
        [Key(9)] public float ClusterCenterX { get; set; }
        [Key(10)] public float ClusterCenterZ { get; set; }

        // 姿勢はクォータニオンの4成分。斜面法線への傾きとランダムYを配置器が持たせている
        // The rotation as the quaternion's four components; the placers give it the slope tilt and a random yaw
        [Key(11)] public float RotationX { get; set; }
        [Key(12)] public float RotationY { get; set; }
        [Key(13)] public float RotationZ { get; set; }
        [Key(14)] public float RotationW { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MapObjectLayoutMessagePack() { }

        public MapObjectLayoutMessagePack(
            int instanceId, string mapObjectGuid, float x, float y, float z,
            float rotationX, float rotationY, float rotationZ, float rotationW,
            float scaleX, float scaleY, float scaleZ,
            int clusterId, float clusterCenterX, float clusterCenterZ)
        {
            InstanceId = instanceId;
            MapObjectGuid = mapObjectGuid;
            X = x;
            Y = y;
            Z = z;
            RotationX = rotationX;
            RotationY = rotationY;
            RotationZ = rotationZ;
            RotationW = rotationW;
            ScaleX = scaleX;
            ScaleY = scaleY;
            ScaleZ = scaleZ;
            ClusterId = clusterId;
            ClusterCenterX = clusterCenterX;
            ClusterCenterZ = clusterCenterZ;
        }
    }
}
