using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Detail;

namespace Game.MapGeneration.Facade
{
    /// <summary>
    ///     生成システム内部のdetailプロトタイプ設定を、境界の外へ出せる仕様へ値のまま写す唯一の場所。
    ///     並びはDetailPrototypeRuntimeConfigCollectorが決めた密度マップと同じ順で、ここでは崩さない
    ///     The single place copying the generation-internal detail prototype configs, values and all, into specs fit to leave the boundary;
    ///     the order is the density-map order DetailPrototypeRuntimeConfigCollector settled and is never disturbed here
    /// </summary>
    public static class DetailPrototypeSpecCollector
    {
        public static List<DetailPrototypeSpec> Collect(IReadOnlyList<DetailPrototypeRuntimeConfig> prototypeConfigs)
        {
            var specs = new List<DetailPrototypeSpec>(prototypeConfigs.Count);
            foreach (var prototypeConfig in prototypeConfigs) specs.Add(ToSpec(prototypeConfig));

            return specs;
        }

        private static DetailPrototypeSpec ToSpec(DetailPrototypeRuntimeConfig prototypeConfig)
        {
            return new DetailPrototypeSpec
            {
                prototypeMeshAddressablePath = prototypeConfig.prototypeMeshAddressablePath,
                prototypeTextureAddressablePath = prototypeConfig.prototypeTextureAddressablePath,
                usePrototypeMesh = prototypeConfig.usePrototypeMesh,
                renderMode = prototypeConfig.renderMode,
                minWidth = prototypeConfig.minWidth,
                maxWidth = prototypeConfig.maxWidth,
                minHeight = prototypeConfig.minHeight,
                maxHeight = prototypeConfig.maxHeight,
                alignToGround = prototypeConfig.alignToGround,
                positionJitter = prototypeConfig.positionJitter,
                targetCoverage = prototypeConfig.targetCoverage,
                holeEdgePadding = prototypeConfig.holeEdgePadding,
                noiseSeed = prototypeConfig.noiseSeed,
                noiseSpread = prototypeConfig.noiseSpread,
                dryColor = prototypeConfig.dryColor,
                healthyColor = prototypeConfig.healthyColor,
                useInstancing = prototypeConfig.useInstancing,
                useDensityScaling = prototypeConfig.useDensityScaling,
            };
        }
    }
}
