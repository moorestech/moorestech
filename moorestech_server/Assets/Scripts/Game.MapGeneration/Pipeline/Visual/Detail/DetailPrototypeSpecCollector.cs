using System.Collections.Generic;
using Game.MapGeneration.Facade;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Visual.Source;

namespace Game.MapGeneration.Pipeline.Visual.Detail
{
    /// <summary>
    ///     バイオームを跨いでdetailプロトタイプ仕様を集める唯一の場所。密度マップと同じ順に並べ、
    ///     この並びを崩すとキャッシュ復元後のプロトタイプが別の密度マップと組み合わさる
    ///     The single place gathering detail prototype specs across biomes, ordered exactly like the density maps;
    ///     breaking this order pairs a cache-restored prototype with the wrong density map
    /// </summary>
    public static class DetailPrototypeSpecCollector
    {
        public static List<DetailPrototypeSpec> Collect(BiomeType[] biomeTypes, BiomeVisualSections visualSections)
        {
            var specs = new List<DetailPrototypeSpec>();

            for (var biomeIndex = 0; biomeIndex < biomeTypes.Length; biomeIndex++)
                foreach (var entry in visualSections.DetailConfigs[biomeIndex].entries)
                    specs.Add(entry.prototypeConfig);

            return specs;
        }
    }
}
