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
        // 区間は半開[tile, tile+size)。閉区間にすると境界上の1本が両隣のタイルで二重に効く
        // The interval is half-open [tile, tile+size); a closed one would let a boundary object act on both neighbouring tiles
        public static List<MapObjectLayoutMessagePack> Slice(
            IReadOnlyList<MapObjectLayoutMessagePack> mapObjects, Vector3 tileWorldPosition,
            float tileWidth, float tileLength)
        {
            var tileLocalObjects = new List<MapObjectLayoutMessagePack>();
            foreach (var mapObject in mapObjects)
            {
                var localX = mapObject.X - tileWorldPosition.x;
                var localZ = mapObject.Z - tileWorldPosition.z;
                if (localX < 0f || tileWidth <= localX || localZ < 0f || tileLength <= localZ) continue;

                // Yはタイル格子の軸ではないので絶対高さのまま残す。XZだけがタイル原点基準へ移る
                // Y is not an axis of the tile lattice and stays an absolute height; only XZ move onto the tile origin
                tileLocalObjects.Add(new MapObjectLayoutMessagePack(
                    mapObject.InstanceId, mapObject.MapObjectGuid, localX, mapObject.Y, localZ));
            }

            return tileLocalObjects;
        }
    }
}
