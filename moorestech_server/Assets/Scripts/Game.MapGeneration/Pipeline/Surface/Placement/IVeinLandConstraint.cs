namespace Game.MapGeneration.Pipeline.Surface.Placement
{
    public interface IVeinLandConstraint
    {
        bool Accept(PlacedVein noiseSpaceVein);
        void ReportRejections(int seed, int tileX, int tileZ, string entryGuid);
    }
}
