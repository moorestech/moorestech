using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Visual.Source;

namespace Game.MapGeneration.Pipeline.Visual.Detail
{
    /// <summary>
    ///     バイオームを跨いでdetailプロトタイプ設定を集める唯一の場所。密度マップと同じ順に並べ、
    ///     この並びを崩すとキャッシュ復元後のプロトタイプが別の密度マップと組み合わさる
    ///     The single place gathering detail prototype configs across biomes, ordered exactly like the density maps;
    ///     breaking this order pairs a cache-restored prototype with the wrong density map
    /// </summary>
    public static class DetailPrototypeRuntimeConfigCollector
    {
        public static List<DetailPrototypeRuntimeConfig> Collect(BiomeType[] biomeTypes, BiomeVisualSections visualSections)
        {
            var prototypeConfigs = new List<DetailPrototypeRuntimeConfig>();

            for (var biomeIndex = 0; biomeIndex < biomeTypes.Length; biomeIndex++)
                foreach (var entry in visualSections.DetailConfigs[biomeIndex].entries)
                    prototypeConfigs.Add(entry.prototypeConfig);

            return prototypeConfigs;
        }
    }
}
