using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Biomes;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     detailプロトタイプの並びを決める唯一の場所。密度マップは常にこの並びと1対1で対応するため、
    ///     キャッシュから密度マップだけを復元してもプロトタイプはここから引き直せる
    ///     The single place fixing the detail prototype order; density maps always correspond one-to-one with it,
    ///     so restoring only the maps from cache still lets the prototypes be rebuilt here
    /// </summary>
    public static class TerrainDetailPrototypeList
    {
        public static List<DetailPrototype> Build(BiomeType[] biomeTypes, BiomeVisualSections visualSections)
        {
            var prototypes = new List<DetailPrototype>();

            for (var biomeIndex = 0; biomeIndex < biomeTypes.Length; biomeIndex++)
                foreach (var entry in visualSections.DetailConfigs[biomeIndex].entries)
                {
                    // 未解決のエントリを読み飛ばすとアドレス整備漏れが「草が生えない」形でしか現れない。ここで落とす
                    // Skipping an unresolved entry would surface a missing address only as absent grass, so it fails here instead
                    entry.prototypeConfig.ThrowIfUnresolved();
                    prototypes.Add(entry.prototypeConfig.ToDetailPrototype());
                }

            return prototypes;
        }
    }
}
