using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Generators;

namespace Game.MapGeneration.Pipeline.Surface.Placement
{
    // 旧版の候補列と乱数消費を変えない規則。陸地では却下せず、重なりは3次元AABBで見る
    // Keeps legacy candidates and random consumption: no land rejection, overlap judged by 3D AABBs
    internal sealed class LegacyVeinPlacementRule : IVeinPlacementRule
    {
        public void BeginCluster()
        {
            // 旧配置の観測は候補列や乱数を変更しない
            // Legacy observation leaves candidates and RNG untouched
        }

        public bool TryAcceptMember(PlacedVein candidate, IReadOnlyList<PlacedVein> excludedVeins, IReadOnlyList<PlacedVein> confirmedVeins)
        {
            return !VeinAabbBuilder.OverlapsAny(candidate, excludedVeins) && !VeinAabbBuilder.OverlapsAny(candidate, confirmedVeins);
        }

        public void ReportRejections(int seed, int tileX, int tileZ, string entryGuid)
        {
            // 旧版の候補列と乱数消費を変えず、制約による却下はない
            // Preserve legacy candidates and random consumption; this rule rejects nothing on land
        }
    }
}
