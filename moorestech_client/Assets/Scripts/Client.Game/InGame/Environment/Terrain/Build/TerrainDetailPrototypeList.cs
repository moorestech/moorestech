using System;
using System.Collections.Generic;
using Game.MapGeneration.Facade;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     detailプロトタイプ仕様(DetailPrototypeSpec)を、解決済みアセット辞書と組み合わせてUnityのDetailPrototypeへ変換する。
    ///     並びの決定自体はGame.MapGeneration.Pipeline.Visual.Detail.DetailPrototypeSpecCollectorが担う
    ///     Converts detail prototype specs into Unity DetailPrototypes using a resolved asset dictionary;
    ///     deciding the order itself belongs to Game.MapGeneration.Pipeline.Visual.Detail.DetailPrototypeSpecCollector
    /// </summary>
    public static class TerrainDetailPrototypeList
    {
        public static List<DetailPrototype> Build(
            IReadOnlyList<DetailPrototypeSpec> prototypeSpecs, IReadOnlyDictionary<string, UnityEngine.Object> resolvedAssets)
        {
            var prototypes = new List<DetailPrototype>();
            foreach (var spec in prototypeSpecs)
                prototypes.Add(ToDetailPrototype(spec, resolvedAssets));

            return prototypes;
        }

        private static DetailPrototype ToDetailPrototype(
            DetailPrototypeSpec spec, IReadOnlyDictionary<string, UnityEngine.Object> resolvedAssets)
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
                detailPrototype.prototype = (GameObject)ResolveAsset(spec.prototypeMeshAddressablePath, resolvedAssets);
            else
                detailPrototype.prototypeTexture = (Texture2D)ResolveAsset(spec.prototypeTextureAddressablePath, resolvedAssets);

            return detailPrototype;
        }

        // 未解決のエントリを読み飛ばすとアドレス整備漏れが「草が生えない」形でしか現れない。ここで落とす
        // Skipping an unresolved entry would surface a missing address only as absent grass, so it fails here instead
        private static UnityEngine.Object ResolveAsset(string address, IReadOnlyDictionary<string, UnityEngine.Object> resolvedAssets)
        {
            if (!resolvedAssets.TryGetValue(address, out var asset) || asset == null)
                throw new InvalidOperationException(
                    $"[TerrainDetailPrototypeList] Detail prototype asset '{address}' was not resolved before detail generation.");

            return asset;
        }
    }
}
