using System;
using System.Collections.Generic;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Surface;
using Mooresmaster.Model.GenerationModule;
using UnityEngine;

namespace Game.MapGeneration.Pipeline
{
    // 版とアルゴリズムの選択を一箇所へ集約する
    // Keep revision and algorithm dispatch in one place
    public static class MapGenerationAlgorithmTable
    {
        private static readonly IReadOnlyDictionary<(string, WorldSurfaceRevision), IMapGenerator> Generators =
            new Dictionary<(string, WorldSurfaceRevision), IMapGenerator>
            {
                { (Generation.AlgorithmConst.VanillaGenerator, WorldSurfaceRevision.Legacy4), new VanillaGenerator() },
                { (Generation.AlgorithmConst.VanillaGenerator, WorldSurfaceRevision.Grounded5), new GroundedVanillaGenerator() },
            };

        public static IMapGenerator Resolve(string algorithm, WorldSurfaceRevision revision)
        {
            if (Generators.TryGetValue((algorithm, revision), out var generator)) return generator;

            // 未知の版を現行生成器で再生成しない
            // Never regenerate unknown revisions with the current generator
            var reason = $"[MapGenerationAlgorithmTable] no generator for algorithm '{algorithm}', revision '{revision}'.";
            Debug.LogWarning(reason);
            throw new InvalidOperationException(reason);
        }
    }
}
