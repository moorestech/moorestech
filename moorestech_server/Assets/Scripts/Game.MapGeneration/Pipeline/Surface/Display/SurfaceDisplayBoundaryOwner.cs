using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal sealed class SurfaceDisplayBoundaryOwner
    {
        private readonly TerrainGenerationConfig _grid;
        private readonly ISurfaceDisplayHeightSource _source;
        private readonly Dictionary<Vector2Int, Dictionary<int, float>> _edges = new();

        internal SurfaceDisplayBoundaryOwner(TerrainGenerationConfig grid, ISurfaceDisplayHeightSource source)
        {
            _grid = grid;
            _source = source;
        }

        internal void CopyTo(float[,] destination, Vector3 scene, PlacementLedger ledger, SurfaceHeightPolicy policy)
        {
            int stride = _grid.Resolution - 1;
            var first = _grid.TileScenePosition(0, 0);
            int tileX = Mathf.RoundToInt((scene.x - first.x) / _grid.terrainWidth);
            int tileZ = Mathf.RoundToInt((scene.z - first.y) / _grid.terrainLength);

            // 頂点の所有者は到着順によらず常に最小側のタイル
            // The vertex owner is always the lower tile, independent of arrival order
            for (int x = 0; x <= stride; x++)
            {
                Copy(x, 0);
                Copy(x, stride);
            }
            for (int z = 1; z < stride; z++)
            {
                Copy(0, z);
                Copy(stride, z);
            }

            #region Internal
            void Copy(int x, int z)
            {
                int globalX = tileX * stride + x;
                int globalZ = tileZ * stride + z;
                int ownerX = SurfaceLattice.OwnerTile(globalX, stride);
                int ownerZ = SurfaceLattice.OwnerTile(globalZ, stride);
                var owner = new Vector2Int(ownerX, ownerZ);
                if (!_edges.TryGetValue(owner, out var edge))
                {
                    edge = Evaluate(ownerX, ownerZ);
                    _edges.Add(owner, edge);
                }
                int ownerIndex = (globalZ - ownerZ * stride) * _grid.Resolution + globalX - ownerX * stride;
                destination[z, x] = edge[ownerIndex];
            }

            Dictionary<int, float> Evaluate(int ownerTileX, int ownerTileZ)
            {
                var ownerConfig = _grid.CreateTileConfig(ownerTileX, ownerTileZ);
                var ownerTile = _grid.TileScenePosition(ownerTileX, ownerTileZ);
                var ownerScene = new Vector3(ownerTile.x, 0f, ownerTile.y);
                var pre = _source.Load(ownerTileX, ownerTileZ);

                // 木の丸めと整地支持セルも所有者の座標で一度だけ評価する
                // Evaluate tree rounding and pad support cells once in the owner's coordinates
                var post = TreePerturbationApplier.Apply(pre, ownerConfig, ownerScene, ledger.Placements);
                post = policy.Apply(post, ownerConfig, ownerScene, ledger);
                var edge = new Dictionary<int, float>();
                int ownerStride = ownerConfig.Resolution - 1;
                for (int z = 0; z <= ownerStride; z++)
                for (int x = 0; x <= ownerStride; x++)
                {
                    if (x != 0 && z != 0 && x != ownerStride && z != ownerStride) continue;
                    edge.Add(z * ownerConfig.Resolution + x, post[z, x]);
                }
                return edge;
            }

            #endregion
        }
    }
}
