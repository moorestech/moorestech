using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Tiling
{
    // 確定済みタイルの配置をワールド座標で溜め、次のタイルの近傍グリッドへ halo として注ぎ直す1種類ぶんの帳面。
    // 木の高さ摂動や surround テクスチャが halo 切り出しで隣タイルを拾うのと同じ扱いを、配置そのものへ広げる。
    // One kind's ledger of confirmed placements in world space, poured back into the next tile's neighbour grid as a halo.
    // It extends to placement itself the same treatment tree perturbation and surround textures already give their haloes.
    public class PlacementHaloChannel
    {
        private readonly List<Vector2> _worldPositions = new List<Vector2>();

        public void Add(float worldX, float worldZ)
        {
            _worldPositions.Add(new Vector2(worldX, worldZ));
        }

        // 配置結果を控える。worldOffset は entries がタイルローカルで持つぶんの補正で、ワールド座標なら 0 を渡す。
        // Records placements; worldOffset corrects entries held in tile-local space and is zero for world-space ones.
        public void AddPlacements(IReadOnlyList<PlacementEntry> entries, float worldOffsetX, float worldOffsetZ)
        {
            if (entries == null) return;
            foreach (var entry in entries)
                Add(entry.WorldPosition.x + worldOffsetX, entry.WorldPosition.z + worldOffsetZ);
        }

        // タイル矩形から radius 以内の点だけをタイルローカル座標へ直して grid へ入れる。
        // SpatialGrid はセル添字だけを Clamp して座標そのものは実値で保持するため、矩形外の点でも距離判定は正確。
        // Seeds grid with the points within radius of the tile rectangle, converted to tile-local coordinates.
        // SpatialGrid clamps only the cell index and keeps the true coordinate, so points outside the rectangle still measure exactly.
        public void SeedGrid(
            SpatialGrid grid, float worldOffsetX, float worldOffsetZ,
            float tileWidth, float tileLength, float radius)
        {
            if (radius <= 0f) return;
            foreach (var position in _worldPositions)
            {
                var localX = position.x - worldOffsetX;
                var localZ = position.y - worldOffsetZ;
                if (localX < -radius || tileWidth + radius < localX) continue;
                if (localZ < -radius || tileLength + radius < localZ) continue;
                grid.Add(localX, localZ);
            }
        }
    }
}
