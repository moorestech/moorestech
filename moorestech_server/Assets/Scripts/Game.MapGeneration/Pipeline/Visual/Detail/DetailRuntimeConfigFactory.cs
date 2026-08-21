using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Detail.Filter;
using UnityEngine;
using GenDetailConfig = Mooresmaster.Model.BiomeDetailConfigModule.BiomeDetailConfig;
using GenDetailFilter = Mooresmaster.Model.DetailFilterModule.DetailFilter;
using GenDetailNoiseLayer = Mooresmaster.Model.DetailNoiseLayerModule.DetailNoiseLayer;

namespace Game.MapGeneration.Pipeline.Visual.Detail
{
    /// <summary>
    ///     GenerationMaster の biomeDetailConfig を実行時 POCO へ変換する。アセットはアドレスのみ写す
    ///     Converts GenerationMaster's biomeDetailConfig into runtime POCOs, carrying assets as addresses only
    /// </summary>
    public static class DetailRuntimeConfigFactory
    {
        public static BiomeDetailConfig Build(GenDetailConfig generated)
        {
            var entries = new List<DetailEntry>();
            foreach (var generatedEntry in generated.Entries)
                entries.Add(ToDetailEntry(generatedEntry));

            return new BiomeDetailConfig
            {
                entries = entries.ToArray(),
                filterRejectThreshold = generated.FilterRejectThreshold,
                borderMargin = generated.BorderMargin,
            };
        }

        private static DetailEntry ToDetailEntry(Mooresmaster.Model.BiomeDetailConfigModule.DetailEntryElement generated)
        {
            return new DetailEntry
            {
                prototypeConfig = ToPrototypeConfig(generated.PrototypeConfig),
                weight = generated.Weight,
                weightRange = generated.WeightRange,
                maxDensity = generated.MaxDensity,
                occludedByOthers = generated.OccludedByOthers,
                noiseStack = ToNoiseStack(generated.NoiseStack),
                slopeFilter = ToFilter(generated.SlopeFilter),
                curvatureFilter = ToFilter(generated.CurvatureFilter),
                angleFilter = ToFilter(generated.AngleFilter),
                treeDistanceFilter = ToFilter(generated.TreeDistanceFilter),
                objectDistanceFilter = ToFilter(generated.ObjectDistanceFilter),
                textureFilter = ToTextureFilter(generated.TextureFilter),
            };
        }

        private static DetailPrototypeConfig ToPrototypeConfig(Mooresmaster.Model.BiomeDetailConfigModule.PrototypeConfig generated)
        {
            // どちらのアドレスが必須かはusePrototypeMeshが決める。必須側の空文字は「意図的に未設定」ではなく整備漏れ
            // usePrototypeMesh decides which address is required; an empty required address is a data gap, not a deliberate blank
            if (generated.UsePrototypeMesh && string.IsNullOrEmpty(generated.PrototypeMeshAddressablePath))
                throw new InvalidOperationException(
                    "[DetailRuntimeConfigFactory] A detail prototype has usePrototypeMesh=true but an empty prototypeMeshAddressablePath.");

            if (!generated.UsePrototypeMesh && string.IsNullOrEmpty(generated.PrototypeTextureAddressablePath))
                throw new InvalidOperationException(
                    "[DetailRuntimeConfigFactory] A detail prototype has usePrototypeMesh=false but an empty prototypeTextureAddressablePath.");

            return new DetailPrototypeConfig
            {
                prototypeMeshAddressablePath = generated.PrototypeMeshAddressablePath,
                prototypeTextureAddressablePath = generated.PrototypeTextureAddressablePath,
                usePrototypeMesh = generated.UsePrototypeMesh,
                renderMode = ParseEnum<DetailRenderMode>(generated.RenderMode, "renderMode"),
                minWidth = generated.MinWidth,
                maxWidth = generated.MaxWidth,
                minHeight = generated.MinHeight,
                maxHeight = generated.MaxHeight,
                alignToGround = generated.AlignToGround,
                positionJitter = generated.PositionJitter,
                targetCoverage = generated.TargetCoverage,
                holeEdgePadding = generated.HoleEdgePadding,
                noiseSeed = generated.NoiseSeed,
                noiseSpread = generated.NoiseSpread,
                dryColor = generated.DryColor,
                healthyColor = generated.HealthyColor,
                useInstancing = generated.UseInstancing,
                useDensityScaling = generated.UseDensityScaling,
            };
        }

        private static DetailNoiseStack ToNoiseStack(Mooresmaster.Model.BiomeDetailConfigModule.NoiseStack generated)
        {
            return new DetailNoiseStack
            {
                primary = ToNoiseLayer(generated.Primary),
                secondary = ToNoiseLayer(generated.Secondary),
                secondaryOp = ParseEnum<NoiseOp>(generated.SecondaryOp, "secondaryOp"),
                tertiary = ToNoiseLayer(generated.Tertiary),
                tertiaryOp = ParseEnum<NoiseOp>(generated.TertiaryOp, "tertiaryOp"),
            };
        }

        private static DetailNoiseLayer ToNoiseLayer(GenDetailNoiseLayer generated)
        {
            return new DetailNoiseLayer
            {
                noiseType = ParseEnum<MapNoiseType>(generated.NoiseType, "noiseType"),
                frequency = generated.Frequency,
                amplitude = generated.Amplitude,
                offset = generated.Offset,
                balance = generated.Balance,
            };
        }

        private static DetailFilter ToFilter(GenDetailFilter generated)
        {
            var mode = ParseEnum<DetailFilter.Mode>(generated.Mode, "mode");

            // Curveモードでキーフレームが空だと毎ピクセルでNREになる。マスタの矛盾は変換時に落とす
            // An empty keyframe array in Curve mode would throw per pixel, so the master conflict fails here instead
            if (mode == DetailFilter.Mode.Curve && (generated.Curve == null || generated.Curve.Length == 0))
                throw new InvalidOperationException(
                    "[DetailRuntimeConfigFactory] A detailFilter in Curve mode has no keyframe: fill its 'curve' array.");

            return new DetailFilter
            {
                enabled = generated.Enabled,
                mode = mode,
                weight = generated.Weight,
                range = generated.Range,
                smoothness = generated.Smoothness,
                noise = ToNoiseLayer(generated.Noise),
                curve = ToAnimationCurve(generated.Curve),
            };
        }

        private static DetailTextureFilter ToTextureFilter(Mooresmaster.Model.BiomeDetailConfigModule.TextureFilter generated)
        {
            var entries = new List<DetailTextureFilter.TextureFilterEntry>();
            foreach (var generatedEntry in generated.Entries)
            {
                // disabledなフィルタのエントリはEvaluateの早期脱出で出力に一切影響しないため、空アドレスを致命化しない
                // A disabled filter's entries never affect the output because Evaluate exits early, so an empty address is not fatal there
                if (generated.Enabled && string.IsNullOrEmpty(generatedEntry.LayerAddressablePath))
                    throw new InvalidOperationException(
                        "[DetailRuntimeConfigFactory] A detail textureFilter entry has an empty layerAddressablePath.");

                entries.Add(new DetailTextureFilter.TextureFilterEntry
                {
                    layerAddressablePath = generatedEntry.LayerAddressablePath,
                    weight = generatedEntry.Weight,
                });
            }

            return new DetailTextureFilter
            {
                enabled = generated.Enabled,
                otherTextureWeight = generated.OtherTextureWeight,
                entries = entries.ToArray(),
            };
        }

        private static AnimationCurve ToAnimationCurve(Mooresmaster.Model.DetailFilterModule.CurveElement[] generatedKeyframes)
        {
            if (generatedKeyframes == null || generatedKeyframes.Length == 0) return null;

            var keyframes = new Keyframe[generatedKeyframes.Length];
            for (var i = 0; i < generatedKeyframes.Length; i++)
            {
                var generated = generatedKeyframes[i];
                keyframes[i] = new Keyframe(generated.Time, generated.Value, generated.InTangent, generated.OutTangent);
            }

            return new AnimationCurve(keyframes);
        }

        // Mooresmaster は enum をオプション名の文字列で生成する。未知名は既定化せず違反名を添えて落とす
        // Mooresmaster emits enums as option-name strings; an unknown name fails loud with the offending value
        private static T ParseEnum<T>(string generatedName, string fieldName) where T : struct
        {
            if (Enum.TryParse<T>(generatedName, out var parsed)) return parsed;
            throw new InvalidOperationException(
                $"[DetailRuntimeConfigFactory] '{fieldName}' has an unrecognized enum value: '{generatedName}' (expected a {typeof(T).Name} option name).");
        }
    }
}
