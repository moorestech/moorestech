using System;
using System.Collections.Generic;
using Game.MapGeneration.Surface;
using UnityEngine;

namespace Game.MapGeneration.Transfer
{
    // 保存版から生成・表示経路を選ぶ唯一の対応表
    // The single mapping from saved versions to generation and presentation revisions
    public static class WorldGeneratorVersion
    {
        public const string Current = "5.0.0";

        // 版キーは地表revisionと転送ファイル構成の両方を表す。転送構成・生成・表示連鎖のどれかを変えたら必ず新しい版キーを足し、旧キーが旧構成でしか読めないならSupportedから外す
        // Supportedにある版は全てこのビルドが生成・転送・表示できる版に限る。読めない版を残すと別ビルドとの組み合わせが冒頭の版照合を素通りし、下流の読み出しずれとして現れる
        // A version key stands for both the surface revision and the transfer file layout: any change to the transfer layout, generation or display chain adds a new key, and a key readable only under the old layout leaves Supported
        // Supported lists only versions this build can generate, transfer and display; keeping an unreadable one lets a mixed-build pairing slip past the up-front version check and surface as misreads downstream
        private static readonly IReadOnlyDictionary<string, WorldSurfaceRevision> Supported =
            new Dictionary<string, WorldSurfaceRevision>
            {
                { "4.0.0", WorldSurfaceRevision.Legacy4 },
                { "5.0.0", WorldSurfaceRevision.Grounded5 }
            };

        public static WorldSurfaceRevision CurrentRevision => Supported[Current];

        public static string SupportedVersionList => string.Join(" and ", Supported.Keys);

        public static bool Supports(string generatorVersion)
        {
            return generatorVersion != null && Supported.ContainsKey(generatorVersion);
        }

        public static WorldSurfaceRevision Resolve(string generatorVersion, string worldId)
        {
            // 未知版を現行版へ置換せず起動境界で止める
            // Reject unknown versions at the startup boundary instead of substituting the current revision
            if (generatorVersion != null && Supported.TryGetValue(generatorVersion, out var revision)) return revision;
            var reason = $"Unsupported generator '{generatorVersion}' for world '{worldId}'; this build supports {SupportedVersionList}; connect to a server on the same build.";
            Debug.LogError(reason);
            throw new InvalidOperationException(reason);
        }

        public static void ThrowIfUnsupported(string generatorVersion, string worldId)
        {
            Resolve(generatorVersion, worldId);
        }

    }
}
