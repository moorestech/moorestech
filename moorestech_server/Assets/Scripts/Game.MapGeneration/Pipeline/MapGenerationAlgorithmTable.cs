using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Mooresmaster.Model.GenerationModule;

namespace Game.MapGeneration.Pipeline
{
    // アルゴリズム enum 名 → 生成器実装のディスパッチテーブル（生成器選択の真実源）。
    // P1 では VanillaGenerator の1件のみ登録し、未登録名は即例外にする。
    // Dispatch table from algorithm enum name to generator impl (single source of truth for selection).
    // P1 registers only VanillaGenerator; unregistered names throw immediately.
    public static class MapGenerationAlgorithmTable
    {
        static readonly IReadOnlyDictionary<string, Func<TerrainGenerationConfig, MapGenerationOutput>> Generators =
            new Dictionary<string, Func<TerrainGenerationConfig, MapGenerationOutput>>
            {
                { Generation.AlgorithmConst.VanillaGenerator, VanillaGenerator.Generate },
            };

        public static Func<TerrainGenerationConfig, MapGenerationOutput> Resolve(string algorithm)
        {
            if (Generators.TryGetValue(algorithm, out var generator)) return generator;
            throw new InvalidOperationException(
                $"[MapGenerationAlgorithmTable] no generator registered for algorithm '{algorithm}'.");
        }
    }
}
