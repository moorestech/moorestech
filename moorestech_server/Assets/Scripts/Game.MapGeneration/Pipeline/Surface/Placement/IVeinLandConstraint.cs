using System.Collections.Generic;

namespace Game.MapGeneration.Pipeline.Surface.Placement
{
    public interface IVeinLandConstraint
    {
        void RecordEligibleCenter();
        bool Overlaps(PlacedVein candidate, IReadOnlyList<PlacedVein> veins);
        bool Accept(PlacedVein noiseSpaceVein);
        void ReportRejections(int seed, int tileX, int tileZ, string entryGuid, int acceptedCount);
    }
}
