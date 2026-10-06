using System;
using System.Collections.Generic;
using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline.Surface;
using Mooresmaster.Model.GenerationModule;
using UnityEngine;

namespace Game.MapGeneration.Pipeline
{
    // 生成と表示の版選択を同じ対応表へ集約する
    // Resolve generation and presentation revisions through the same registry
    public static class MapGenerationAlgorithmTable
    {
        private static readonly IReadOnlyDictionary<WorldSurfaceRevision, SurfaceRevisionPolicy> Revisions =
            new Dictionary<WorldSurfaceRevision, SurfaceRevisionPolicy>
            {
                { WorldSurfaceRevision.Legacy4, new SurfaceRevisionPolicy.Legacy() },
                { WorldSurfaceRevision.Grounded5, new SurfaceRevisionPolicy.Grounded() },
            };

        public static IMapGenerator Resolve(string algorithm, WorldSurfaceRevision revision)
        {
            if (algorithm == Generation.AlgorithmConst.VanillaGenerator)
                return ResolveSurface(revision).Generator;

            var reason = $"[MapGenerationAlgorithmTable] no generator for algorithm '{algorithm}', revision '{revision}'.";
            Debug.LogError(reason);
            throw new InvalidOperationException(reason);
        }

        public static SurfaceRevisionPolicy ResolveSurface(WorldSurfaceRevision revision)
        {
            if (Revisions.TryGetValue(revision, out var policy)) return policy;

            // 未知の版を既存表示へ置換しない
            // Never substitute existing presentation for an unknown revision
            var reason = $"Unsupported surface revision '{revision}'.";
            Debug.LogError(reason);
            throw new InvalidOperationException(reason);
        }
    }
}
