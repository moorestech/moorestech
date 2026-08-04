using Game.MapGeneration.Pipeline.Config;
using UnityEngine;
using GenNoise = Mooresmaster.Model.PlacementNoiseModule.PlacementNoise;
using GenFilter = Mooresmaster.Model.PlacementFilterModule.PlacementFilter;

namespace Game.MapGeneration.Pipeline.Runtime
{
    // 生成型 PlacementNoise/PlacementFilter → 実行時 POCO 変換。texture/channel はスキーマ化で
    // 未使用のため写さない。curve は keyframe 配列から AnimationCurve を再構築する。
    // Converts generated PlacementNoise/PlacementFilter to runtime POCOs. texture/channel are
    // unused post-migration and skipped; curve is rebuilt into an AnimationCurve from keyframes.
    internal static class PlacementRefConvert
    {
        public static PlacementNoise ToPlacementNoise(GenNoise gen)
        {
            return new PlacementNoise
            {
                noiseType = RuntimeConvert.ToMapNoiseType(gen.NoiseType),
                frequency = gen.Frequency,
                amplitude = gen.Amplitude,
                offset = gen.Offset,
                balance = gen.Balance
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
