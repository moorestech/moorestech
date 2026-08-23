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
        public readonly Vector3 Scale;

        // 地形の見た目への効き方（台帳の種別をそのまま運ぶ）
        // How the placement affects the terrain's look (carried straight from the ledger)
        public readonly TerrainSurroundEffectType SurroundEffect;

        // クラスタ識別子と重心（タイルローカル化済み）。独立配置はnull
        // The cluster identifier and its centroid, rebased onto the tile-local frame; an independent placement is null
        public readonly PlacementCluster? LocalCluster;

        public TileLocalPlacement(
            string guid, Vector3 localPosition, Vector3 scale,
            TerrainSurroundEffectType surroundEffect, PlacementCluster? localCluster)
        {
            Guid = guid;
            LocalPosition = localPosition;
            Scale = scale;
            SurroundEffect = surroundEffect;
            LocalCluster = localCluster;
        }
    }
}
