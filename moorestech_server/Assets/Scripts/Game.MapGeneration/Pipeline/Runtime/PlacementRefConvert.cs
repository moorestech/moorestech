using System;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;
using GenNoise = Mooresmaster.Model.PlacementNoiseModule.PlacementNoise;
using GenFilter = Mooresmaster.Model.PlacementFilterModule.PlacementFilter;

namespace Game.MapGeneration.Pipeline.Runtime
{
    // 生成型 PlacementNoise/PlacementFilter → 実行時 POCO 変換。texturePngPath は文字列のまま写し、
    // 画素への展開は生成直前の PlacementNoiseTextureResolver が行う。curve は keyframe 配列から再構築。
    // Converts generated PlacementNoise/PlacementFilter to runtime POCOs; texturePngPath is copied as a
    // string and expanded to pixels later by PlacementNoiseTextureResolver. curve is rebuilt from keyframes.
    internal static class PlacementRefConvert
    {
        public static PlacementNoise ToPlacementNoise(GenNoise gen)
        {
            // 源は手続きかテクスチャのどちらか一方。両指定は展開後にテクスチャが黙って勝ち、frequency が無言で捨てられる。
            // A source is procedural or texture, never both: specifying both silently lets the texture win and drops frequency.
            var noiseType = RuntimeConvert.ToMapNoiseType(gen.NoiseType);
            if (!string.IsNullOrEmpty(gen.TexturePngPath) && noiseType != MapNoiseType.None)
                throw new InvalidOperationException(
                    $"[PlacementRefConvert] placementNoise specifies texturePngPath '{gen.TexturePngPath}' together with noiseType {noiseType}.");

            return new PlacementNoise
            {
                noiseType = noiseType,
                frequency = gen.Frequency,
                amplitude = gen.Amplitude,
                offset = gen.Offset,
                balance = gen.Balance,
                texturePngPath = gen.TexturePngPath,
                channel = RuntimeConvert.ToTextureChannel(gen.Channel)
            };
        }

        public static PlacementFilter ToPlacementFilter(GenFilter gen)
        {
            // 生成済みキーフレームを具体型のまま写し、空配列は線形扱いのnullにする。
            // Copy concrete generated keyframes directly, keeping an empty array as null for linear behavior.
            AnimationCurve curve = null;
            if (gen.Curve != null && 0 < gen.Curve.Length)
            {
                var keys = new Keyframe[gen.Curve.Length];
                for (var i = 0; i < gen.Curve.Length; i++)
                {
                    var keyframe = gen.Curve[i];
                    keys[i] = new Keyframe(keyframe.Time, keyframe.Value, keyframe.InTangent, keyframe.OutTangent);
                }
                curve = new AnimationCurve(keys);
            }

            return new PlacementFilter
            {
                enabled = gen.Enabled,
                range = gen.Range,
                smoothness = gen.Smoothness,
                noise = ToPlacementNoise(gen.Noise),
                curve = curve
            };
        }
    }
}
