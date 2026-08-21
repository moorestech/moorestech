using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     detailプロトタイプ仕様のアドレスをAddressablesで解決し、Unity DetailPrototypeへ組み立てる。
    ///     並びの決定自体はGame.MapGeneration.Pipeline.Visual.Detail.DetailPrototypeSpecCollectorが担う
    ///     Resolves a detail prototype spec's address via Addressables and assembles a Unity DetailPrototype;
    ///     deciding the order itself belongs to Game.MapGeneration.Pipeline.Visual.Detail.DetailPrototypeSpecCollector
    /// </summary>
    public static class DetailPrototypeAssetResolver
    {
        public static async UniTask<List<DetailPrototype>> ResolveAsync(IReadOnlyList<DetailPrototypeSpec> prototypeSpecs)
        {
            var detailPrototypes = new List<DetailPrototype>();
            foreach (var spec in prototypeSpecs)
                detailPrototypes.Add(await ResolveOneAsync(spec));

            return detailPrototypes;
        }

        private static async UniTask<DetailPrototype> ResolveOneAsync(DetailPrototypeSpec spec)
        {
            var detailPrototype = new DetailPrototype
            {
                renderMode = spec.renderMode,
                minWidth = spec.minWidth,
                maxWidth = spec.maxWidth,
                minHeight = spec.minHeight,
                maxHeight = spec.maxHeight,
                noiseSeed = spec.noiseSeed,
                noiseSpread = spec.noiseSpread,
                dryColor = spec.dryColor,
                healthyColor = spec.healthyColor,
                useInstancing = spec.useInstancing,
                usePrototypeMesh = spec.usePrototypeMesh,
                alignToGround = spec.alignToGround,
                positionJitter = spec.positionJitter,
                targetCoverage = spec.targetCoverage,
                holeEdgePadding = spec.holeEdgePadding,
                useDensityScaling = spec.useDensityScaling,
            };

            if (spec.usePrototypeMesh)
                detailPrototype.prototype = await LoadAsync<GameObject>(spec.prototypeMeshAddressablePath);
            else
                detailPrototype.prototypeTexture = await LoadAsync<Texture2D>(spec.prototypeTextureAddressablePath);

            return detailPrototype;

            #region Internal

            // 未解決のエントリを読み飛ばすとアドレス整備漏れが「草が生えない」形でしか現れない。ここで落とす
            // Skipping an unresolved entry would surface a missing address only as absent grass, so it fails here instead
            async UniTask<T> LoadAsync<T>(string address) where T : UnityEngine.Object
            {
                var asset = await AddressableLoader.LoadAsyncDefault<T>(address);
                if (asset == null)
                    throw new InvalidOperationException(
                        $"[DetailPrototypeAssetResolver] Detail prototype asset '{address}' was not resolved before detail generation.");
                return asset;
            }

            #endregion
        }
    }
}
