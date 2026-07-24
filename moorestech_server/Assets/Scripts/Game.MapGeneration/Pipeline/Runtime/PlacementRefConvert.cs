using Game.MapGeneration.Pipeline.Config;
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
            return new PlacementFilter
            {
                enabled = gen.Enabled,
                range = gen.Range,
                smoothness = gen.Smoothness,
                noise = ToPlacementNoise(gen.Noise),
                curve = RuntimeConvert.ToAnimationCurve(gen.Curve,
                    k => k.Time, k => k.Value, k => k.InTangent, k => k.OutTangent)
            };
        }
    }
}
