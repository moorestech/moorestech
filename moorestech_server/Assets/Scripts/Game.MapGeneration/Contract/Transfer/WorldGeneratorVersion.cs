using System;
using System.Collections.Generic;
using Game.MapGeneration.Facade.Surface;
using UnityEngine;

namespace Game.MapGeneration.Transfer
{
    // 保存版から生成・表示経路を選ぶ唯一の対応表
    // The single mapping from saved versions to generation and presentation revisions
    public static class WorldGeneratorVersion
    {
        public const string Current = "5.0.0";
        private static readonly IReadOnlyDictionary<string, WorldSurfaceRevision> Supported =
            new Dictionary<string, WorldSurfaceRevision>
            {
                { "4.0.0", WorldSurfaceRevision.Legacy4 },
                { "5.0.0", WorldSurfaceRevision.Grounded5 }
            };

        public static WorldSurfaceRevision CurrentRevision => Supported[Current];

        public static bool Supports(string generatorVersion)
        {
            return generatorVersion != null && Supported.ContainsKey(generatorVersion);
        }

        public static WorldSurfaceRevision Resolve(string generatorVersion, string worldId)
        {
            // 未知版を現行版へ置換せず起動境界で止める
            // Reject unknown versions at the startup boundary instead of substituting the current revision
            if (generatorVersion != null && Supported.TryGetValue(generatorVersion, out var revision)) return revision;
            var reason = $"Unsupported generator '{generatorVersion}' for world '{worldId}'; connect to a server on the same build.";
            Debug.LogError(reason);
            throw new InvalidOperationException(reason);
        }

        public static void ThrowIfUnsupported(string generatorVersion, string worldId)
        {
            Resolve(generatorVersion, worldId);
        }

    }
}
