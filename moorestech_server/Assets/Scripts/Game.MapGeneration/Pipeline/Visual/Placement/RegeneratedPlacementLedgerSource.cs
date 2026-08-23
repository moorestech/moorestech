using Game.MapGeneration.Pipeline.Config;
using Mooresmaster.Model.GenerationModule;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    /// <summary>
    ///     台帳を持たない側（クライアント）が、要求されたときだけpass-1を回して台帳を作る口
    ///     全タイルがキャッシュに載っていれば1度も回らない。回った結果は同一起動中で使い回す
    ///     The port for a side that owns no ledger (the client), running pass-1 only when asked
    ///     With every tile in the cache it never runs at all, and a run's result is reused for the rest of the session
    /// </summary>
    public class RegeneratedPlacementLedgerSource : IPlacementLedgerSource
    {
        private readonly Generation _selectedGeneration;
        private readonly TerrainGenerationConfig _config;
        private PlacementLedger _resolvedLedger;

        public RegeneratedPlacementLedgerSource(Generation selectedGeneration, TerrainGenerationConfig config)
        {
            _selectedGeneration = selectedGeneration;
            _config = config;
        }

        public PlacementLedger Resolve()
        {
            _resolvedLedger ??= MapGenerationPipeline.Generate(_selectedGeneration, _config).Ledger;
            return _resolvedLedger;
        }
    }
}
