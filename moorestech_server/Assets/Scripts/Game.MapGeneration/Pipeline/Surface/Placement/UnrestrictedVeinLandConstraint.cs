using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Generators;
namespace Game.MapGeneration.Pipeline.Surface.Placement
{
    public sealed class UnrestrictedVeinLandConstraint : IVeinLandConstraint
    {
        public void RecordEligibleCenter()
        {
            // 旧配置の観測は候補列や乱数を変更しない
            // Legacy observation leaves candidates and RNG untouched
        }

        public bool Overlaps(PlacedVein candidate, IReadOnlyList<PlacedVein> veins)
        {
            return VeinAabbBuilder.OverlapsAny(candidate, veins);
        }

        public bool Accept(PlacedVein noiseSpaceVein)
        {
            return true;
        }

        public void ReportRejections(int seed, int tileX, int tileZ, string entryGuid, int acceptedCount)
        {
            // 旧版の候補列と乱数消費を変えず、制約による却下はない
            // Preserve legacy candidates and random consumption; this constraint rejects nothing
        }
    }
}
