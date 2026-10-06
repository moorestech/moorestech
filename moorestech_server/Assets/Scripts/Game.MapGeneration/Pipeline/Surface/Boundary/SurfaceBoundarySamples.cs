using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    // 全域格子の境界値は最小Z→Xのタイルだけが生成する
    // Only the tile with the smallest Z then X emits each shared lattice vertex
    internal sealed class SurfaceBoundarySamples
    {
        private readonly TerrainGenerationConfig _config;
        private readonly int _biomeCount;
        private readonly Dictionary<Vector2Int, SurfaceBoundarySample> _owned = new();

        internal SurfaceBoundarySamples(TerrainGenerationConfig config, int biomeCount)
        {
            _config = config;
            _biomeCount = biomeCount;
        }

        internal void CaptureOwned(int tileX, int tileZ, JobBuffers source)
        {
            int stride = _config.Resolution - 1;
            foreach (var local in SharedVertices(tileX, tileZ))
            {
                var global = new Vector2Int(tileX * stride + local.x, tileZ * stride + local.y);
                int ownerX = Mathf.Max(0, (global.x - 1) / stride);
                int ownerZ = Mathf.Max(0, (global.y - 1) / stride);
                if (ownerX != tileX || ownerZ != tileZ) continue;
                _owned.Add(global, new SurfaceBoundarySample(source, local.y * _config.Resolution + local.x, _biomeCount));
            }
        }

        internal void Emit(int tileX, int tileZ, JobBuffers destination)
        {
            int stride = _config.Resolution - 1;
            foreach (var local in SharedVertices(tileX, tileZ))
            {
                var global = new Vector2Int(tileX * stride + local.x, tileZ * stride + local.y);
                if (!_owned.TryGetValue(global, out var sample))
                    throw SurfaceGenerationValidation.Failure(_config, $"{tileX},{tileZ}",
                        $"Boundary owner has not emitted vertex {global.x},{global.y}.");
                sample.Write(destination, local.y * _config.Resolution + local.x);
            }
        }

        private IEnumerable<Vector2Int> SharedVertices(int tileX, int tileZ)
        {
            int stride = _config.Resolution - 1;
            // 内部境界だけを保持し、角は一度だけ扱う
            // Retain only internal boundaries and visit each corner exactly once
            for (int x = 0; x <= stride; x++)
            {
                if (tileZ > 0) yield return new Vector2Int(x, 0);
                if (tileZ < _config.gridSizeZ - 1) yield return new Vector2Int(x, stride);
            }
            for (int z = 0; z <= stride; z++)
            {
                if ((z == 0 && tileZ > 0) || (z == stride && tileZ < _config.gridSizeZ - 1)) continue;
                if (tileX > 0) yield return new Vector2Int(0, z);
                if (tileX < _config.gridSizeX - 1) yield return new Vector2Int(stride, z);
            }
        }
    }
}
