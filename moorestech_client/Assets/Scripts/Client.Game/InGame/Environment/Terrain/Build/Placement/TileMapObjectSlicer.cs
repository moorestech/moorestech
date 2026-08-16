using System.Collections.Generic;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build.Placement
{
    /// <summary>
    ///     シーン絶対座標で届くMapObjectsから1タイルぶんを切り出し、タイルローカル座標へ寄せ直す。
    ///     タイル単位で回る見た目の再構築（木の高さ摂動・距離場・根元テクスチャ）が共通で必要とする前処理
    ///     Slices one tile out of the scene-absolute MapObjects and rebases them to tile-local coordinates;
    ///     the shared preprocessing every per-tile visual rebuild needs (tree height perturbation, distance fields, root textures)
    /// </summary>
    public static class TileMapObjectSlicer
    {
        // 基本の窓は半開[tile, tile+size)。閉区間にすると境界上の1本が両隣のタイルで二重に効く
        // The base window is half-open [tile, tile+size); a closed one would let a boundary object act on both neighbouring tiles
        // haloはその外側へ広げる幅。境界の外の木が消えると、境界に沿って効き方が変わる帯や高さの段差ができる
        // The halo widens it outwards; dropping the trees just outside bands the effect along the seam or steps the height there
        // halo内のタイル外の点はローカル座標で負値やtileWidth超になる。受け手はその座標のまま真の距離で測る責任を持つ
        // Points inside the halo but outside the tile go negative or past tileWidth in local coordinates, and the receiver must measure true distances from them as they are
        public static List<MapObjectLayoutMessagePack> SliceWithHalo(
            IReadOnlyList<MapObjectLayoutMessagePack> mapObjects, Vector3 tileWorldPosition,
            float tileWidth, float tileLength, float halo)
        {
            var tileLocalObjects = new List<MapObjectLayoutMessagePack>();
            foreach (var mapObject in mapObjects)
            {
                var localX = mapObject.X - tileWorldPosition.x;
                var localZ = mapObject.Z - tileWorldPosition.z;
                if (localX < -halo || tileWidth + halo <= localX || localZ < -halo || tileLength + halo <= localZ) continue;

                // Yはタイル格子の軸ではないので絶対高さのまま残す。XZだけがタイル原点基準へ移る
                // Y is not an axis of the tile lattice and stays an absolute height; only XZ move onto the tile origin
                // クラスタ重心も位置と同じくタイルローカル化する。独立配置(-1)は未使用値(0,0)のまま据え置く
                // The cluster centroid is rebased the same as position; an independent placement (-1) keeps its unused (0,0)
                var hasCluster = 0 <= mapObject.ClusterId;
                var localClusterCenterX = hasCluster ? mapObject.ClusterCenterX - tileWorldPosition.x : mapObject.ClusterCenterX;
                var localClusterCenterZ = hasCluster ? mapObject.ClusterCenterZ - tileWorldPosition.z : mapObject.ClusterCenterZ;

                // 姿勢はタイル格子と無関係なのでそのまま運ぶ。落とすと切り出し後の見た目が向きを失う
                // The rotation is unrelated to the tile lattice and rides along untouched; dropping it loses the orientation downstream
                tileLocalObjects.Add(new MapObjectLayoutMessagePack(
                    mapObject.InstanceId, mapObject.MapObjectGuid, localX, mapObject.Y, localZ,
                    mapObject.RotationX, mapObject.RotationY, mapObject.RotationZ, mapObject.RotationW,
                    mapObject.ScaleX, mapObject.ScaleY, mapObject.ScaleZ,
                    mapObject.ClusterId, localClusterCenterX, localClusterCenterZ));
            }

            return tileLocalObjects;
        }
    }
}
