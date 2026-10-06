namespace Game.MapGeneration.Pipeline.Surface.Placement
{
    public sealed class UnrestrictedVeinLandConstraint : IVeinLandConstraint
    {
        public bool Accept(PlacedVein noiseSpaceVein)
        {
            return true;
        }

        public void ReportRejections(int seed, int tileX, int tileZ, string entryGuid)
        {
            // 旧版の候補列と乱数消費を変えず、制約による却下はない
            // Preserve legacy candidates and random consumption; this constraint rejects nothing
        }
    }
}
