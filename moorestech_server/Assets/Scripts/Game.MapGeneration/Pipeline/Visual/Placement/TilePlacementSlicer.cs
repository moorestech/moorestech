using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    /// <summary>
    ///     シーン絶対座標で届く配置台帳から1タイルぶんを切り出し、タイルローカル座標へ寄せ直す。
    ///     タイル単位で回る見た目の再構築（木の高さ摂動・距離場・根元テクスチャ）が共通で必要とする前処理
    ///     Slices one tile out of the scene-absolute placement ledger and rebases it to tile-local coordinates;
    ///     the shared preprocessing every per-tile visual rebuild needs (tree height perturbation, distance fields, root textures)
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
                // クラスタ重心も位置と同じくタイルローカル化する。独立配置(-1)は未使用値(0,0)のまま据え置く
                // The cluster centroid is rebased the same as position; an independent placement (-1) keeps its unused (0,0)
                var hasCluster = 0 <= placement.ClusterId;
                var localClusterCenterX = hasCluster ? placement.ClusterCenter.x - tileWorldPosition.x : placement.ClusterCenter.x;
                var localClusterCenterZ = hasCluster ? placement.ClusterCenter.y - tileWorldPosition.z : placement.ClusterCenter.y;

                // 姿勢はタイル格子と無関係なのでそのまま運ぶ。落とすと切り出し後の見た目が向きを失う
                // The rotation is unrelated to the tile lattice and rides along untouched; dropping it loses the orientation downstream
                tileLocalPlacements.Add(new TileLocalPlacement(
                    placement.Guid,
                    new Vector3(localX, placement.ScenePosition.y, localZ),
                    placement.Rotation, placement.Scale, placement.SurroundEffect,
                    placement.ClusterId, new Vector2(localClusterCenterX, localClusterCenterZ)));
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
            // stonesは岩用距離場を担う全岩、bareGroundStonesはその中で裸地を塗る岩だけ（移植元はBoulder/Cliff名の岩のみ裸地化する）
            // stones carries every rock for the rock distance field; bareGroundStones is the subset that paints bare ground (the source repaints only Boulder/Cliff rocks)
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
