using System.Globalization;
using Game.MapGeneration.Pipeline.Surface.Grading;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    internal static class GroundingPadDigest
    {
        internal static string Describe(VeinGroundingPad pad)
        {
            // padだけに接頭辞を付け、旧配置行の形式を保つ
            // Prefix only pad rows and retain the legacy placement row format
            return string.Join("|", "pad", Format(pad.Core.xMin), Format(pad.Core.yMin),
                Format(pad.Core.xMax), Format(pad.Core.yMax), Format(pad.HeightMeters), Format(pad.BlendWidth));
        }

        private static string Format(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
