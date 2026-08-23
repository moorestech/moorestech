using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    /// <summary>
    ///     絶対座標の配置台帳を1タイル分・ローカル座標へ切り出す
    ///     タイル単位の見た目再構築が共通で使う前処理
    ///     Slices the absolute-coordinate placement ledger into one tile at tile-local coordinates
    ///     Shared preprocessing used by every per-tile visual rebuild
    /// </summary>
    public static class TilePlacementSlicer
    {
        // 基本の窓は半開[tile, tile+size)。閉区間にすると境界上の1本が両隣のタイルで二重に効く
        // The base window is half-open [tile, tile+size); a closed one would let a boundary object act on both neighbouring tiles
        // haloはその外側へ広げる幅。境界の外の木が消えると、境界に沿って効き方が変わる帯や高さの段差ができる
        // The halo widens it outwards; dropping the trees just outside bands the effect along the seam or steps the height there
        // halo内のタイル外の点はローカル座標で負値やtileWidth超になる。受け手はその座標のまま真の距離で測る責任を持つ
        // Points inside the halo but outside the tile go negative or past tileWidth in local coordinates, and the receiver must measure true distances from them as they are
        // 台帳からタイルローカル型への写しはこの1箇所だけ。以降の painter/builder は絶対座標の型を受け取らない
        // This is the only place the ledger is copied into the tile-local type; no painter or builder downstream takes the absolute-frame type
        public static List<TileLocalPlacement> SliceWithHalo(
            IReadOnlyList<LedgerPlacement> placements, Vector3 tileWorldPosition,
            float tileWidth, float tileLength, float halo)
        {
            var tileLocalPlacements = new List<TileLocalPlacement>();
            foreach (var placement in placements)
            {
                var localX = placement.ScenePosition.x - tileWorldPosition.x;
                var localZ = placement.ScenePosition.z - tileWorldPosition.z;
                if (localX < -halo || tileWidth + halo <= localX || localZ < -halo || tileLength + halo <= localZ) continue;

                // Yはタイル格子の軸ではないので絶対高さのまま残す。XZだけがタイル原点基準へ移る
                // Y is not an axis of the tile lattice and stays an absolute height; only XZ move onto the tile origin
                // クラスタ重心もローカル化。独立配置(null)はそのままnull
                // The cluster centroid is rebased too; an independent placement (null) stays null
                PlacementCluster? localCluster = null;
                if (placement.Cluster.HasValue)
                {
                    var cluster = placement.Cluster.Value;
                    localCluster = new PlacementCluster(
                        cluster.Id, new Vector2(cluster.Center.x - tileWorldPosition.x, cluster.Center.y - tileWorldPosition.z));
                }

                tileLocalPlacements.Add(new TileLocalPlacement(
                    placement.Guid,
                    new Vector3(localX, placement.ScenePosition.y, localZ),
                    placement.Scale, placement.SurroundEffect,
                    localCluster));
            }

            return tileLocalPlacements;
        }

        // 切り出しと種別分割は常に対で要る。別々に呼べる形だと呼び出し側が片方だけ別のhaloで回せてしまう
        // Slicing and kind splitting are always needed together; exposing them apart lets a caller run one on a different halo
        public static void SliceKindsWithHalo(
            IReadOnlyList<LedgerPlacement> placements, Vector3 tileWorldPosition,
            float tileWidth, float tileLength, float halo,
            out List<TileLocalPlacement> trees, out List<TileLocalPlacement> stones,
            out List<TileLocalPlacement> bareGroundStones)
        {
            var tileLocalPlacements = SliceWithHalo(placements, tileWorldPosition, tileWidth, tileLength, halo);
            Split(tileLocalPlacements, out trees, out stones, out bareGroundStones);

            #region Internal

            // タイルローカル化された配置物を地形への見た目の効き方で分ける唯一の場所。Detailの距離フィルタは両者を別の距離場として読み、
            // 岩周辺の裸地テクスチャは岩側だけを読むため、混ざるとどちらの規則も相手側へ漏れる
            // The single place splitting tile-local placements by how they affect the terrain's look; the detail distance filters read
            // the two as separate fields and the bare-ground texture reads only the rocks, so mixing them leaks each rule onto the other
            // stones=岩用距離場の全岩、bareGroundStones=裸地化する岩のみ
            // stones = every rock for the distance field, bareGroundStones = only the ones painting bare ground
            void Split(
                IReadOnlyList<TileLocalPlacement> tileLocal,
                out List<TileLocalPlacement> treeList, out List<TileLocalPlacement> stoneList,
                out List<TileLocalPlacement> bareGroundStoneList)
            {
                treeList = new List<TileLocalPlacement>();
                stoneList = new List<TileLocalPlacement>();
                bareGroundStoneList = new List<TileLocalPlacement>();

                foreach (var placement in tileLocal)
                    switch (placement.SurroundEffect)
                    {
                        case TerrainSurroundEffectType.treeRootPatch:
                            treeList.Add(placement);
                            break;
                        case TerrainSurroundEffectType.rockBareGround:
                            stoneList.Add(placement);
                            bareGroundStoneList.Add(placement);
                            break;
                        case TerrainSurroundEffectType.rockNoBareGround:
                            stoneList.Add(placement);
                            break;
                        default:
                            throw new InvalidOperationException(
                                $"[TilePlacementSlicer] Placement {placement.Guid} carries an unknown SurroundEffect {placement.SurroundEffect}.");
                    }
            }

            #endregion
        }
    }
}
