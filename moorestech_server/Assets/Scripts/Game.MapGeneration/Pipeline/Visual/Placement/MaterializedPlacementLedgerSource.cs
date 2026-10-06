namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    public class MaterializedPlacementLedgerSource : IPlacementLedgerSource
    {
        private readonly GenerationRun _run;

        public MaterializedPlacementLedgerSource(GenerationRun run)
        {
            _run = run;
        }

        public GenerationRun Resolve()
        {
            return _run;
        }
    }
}
