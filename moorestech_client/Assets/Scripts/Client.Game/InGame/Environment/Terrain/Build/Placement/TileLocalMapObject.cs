using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build.Placement
{
    /// <summary>
    ///     タイルローカル座標系に寄せ直された配置物。原点はタイルの角で、halo内のタイル外の点はXZが負値やtileWidth超になる。
    ///     転送DTO(MapObjectLayoutMessagePack)はシーン絶対座標のままなので、同じ型で2つのフレームを運ばないためにこの型がある。
    ///     取り違えは例外にならず、ピクセル索引が範囲外へ落ちて塗りが黙って消えるだけなので型で分けている
    ///     A placement rebased onto the tile-local frame, whose origin is the tile's corner and whose halo points outside the tile go negative or past tileWidth in XZ.
    ///     The transferred DTO (MapObjectLayoutMessagePack) stays scene-absolute, and this type exists so one type never carries both frames.
    ///     A mix-up throws nothing: the pixel index simply falls out of range and the paint vanishes silently, so the two frames are split by type
    /// </summary>
    public readonly struct TileLocalMapObject
    {
        public readonly int InstanceId;
        public readonly string Guid;
        public readonly Vector3 LocalPosition;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        // クラスタ識別子と重心。-1は独立配置で、そのとき重心は未使用値(0,0)のまま据え置かれる
        // The cluster identifier and its centroid; -1 is an independent placement whose centroid keeps its unused (0,0)
        public readonly int ClusterId;
        public readonly Vector2 LocalClusterCenter;

        public TileLocalMapObject(
            int instanceId, string guid, Vector3 localPosition, Quaternion rotation, Vector3 scale,
            int clusterId, Vector2 localClusterCenter)
        {
            InstanceId = instanceId;
            Guid = guid;
            LocalPosition = localPosition;
            Rotation = rotation;
            Scale = scale;
            ClusterId = clusterId;
            LocalClusterCenter = localClusterCenter;
        }
    }
}
