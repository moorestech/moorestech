using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    /// <summary>
    ///     タイルローカル座標系に寄せ直された配置物。原点はタイルの角で、halo内のタイル外の点はXZが負値やtileWidth超になる。
    ///     台帳(LedgerPlacement)はシーン絶対座標のままなので、同じ型で2つのフレームを運ばないためにこの型がある。
    ///     取り違えは例外にならず、ピクセル索引が範囲外へ落ちて塗りが黙って消えるだけなので型で分けている
    ///     A placement rebased onto the tile-local frame, whose origin is the tile's corner and whose halo points outside the tile go negative or past tileWidth in XZ.
    ///     The ledger (LedgerPlacement) stays scene-absolute, and this type exists so one type never carries both frames.
    ///     A mix-up throws nothing: the pixel index simply falls out of range and the paint vanishes silently, so the two frames are split by type
    /// </summary>
    public readonly struct TileLocalPlacement
    {
        public readonly string Guid;
        public readonly Vector3 LocalPosition;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        // 地形の見た目への効き方。台帳が既に持つ種別をそのまま運び、Splitはこの値だけで振り分ける
        // How the placement affects the terrain's look; carried straight from the ledger so Split sorts on this value alone
        public readonly TerrainSurroundEffectType SurroundEffect;

        // クラスタ識別子と重心。-1は独立配置で、そのとき重心は未使用値(0,0)のまま据え置かれる
        // The cluster identifier and its centroid; -1 is an independent placement whose centroid keeps its unused (0,0)
        public readonly int ClusterId;
        public readonly Vector2 LocalClusterCenter;

        public TileLocalPlacement(
            string guid, Vector3 localPosition, Quaternion rotation, Vector3 scale,
            TerrainSurroundEffectType surroundEffect, int clusterId, Vector2 localClusterCenter)
        {
            Guid = guid;
            LocalPosition = localPosition;
            Rotation = rotation;
            Scale = scale;
            SurroundEffect = surroundEffect;
            ClusterId = clusterId;
            LocalClusterCenter = localClusterCenter;
        }
    }
}
