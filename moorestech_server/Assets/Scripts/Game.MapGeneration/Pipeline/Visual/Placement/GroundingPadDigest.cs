using System.Globalization;
using Game.MapGeneration.Pipeline.Surface.Grading;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    internal static class GroundingPadDigest
    {
        internal static string Describe(VeinGroundingPad pad)
        {
            // padだけ接頭辞を付け旧行形式を保つ
            // Prefix only pad rows; keep the legacy row format
            return string.Join("|", "pad", Format(pad.Core.xMin), Format(pad.Core.yMin),
                Format(pad.Core.xMax), Format(pad.Core.yMax), Format(pad.HeightMeters), Format(pad.BlendWidth));

            #region Internal

            string Format(float value)
            {
                return value.ToString("R", CultureInfo.InvariantCulture);
            }

            #endregion
        }
    }
}
