namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    public class MaterializedGenerationRunSource : IGenerationRunSource
    {
        private readonly GenerationRun _run;

        public MaterializedGenerationRunSource(GenerationRun run)
        {
            _run = run;
        }

        public GenerationRun Resolve()
        {
            return _run;
        }
    }
}
