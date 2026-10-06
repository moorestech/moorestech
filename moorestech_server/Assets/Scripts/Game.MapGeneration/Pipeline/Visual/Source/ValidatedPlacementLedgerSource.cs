using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Surround;

namespace Game.MapGeneration.Pipeline.Visual.Source
{
    internal sealed class ValidatedPlacementLedgerSource : IPlacementLedgerSource
    {
        private readonly IPlacementLedgerSource _ledgerSource;
        private readonly string _expectedPlacementLedgerDigest;
        private readonly TreeSurroundSpeciesTable _treeSurroundSpecies;
        private readonly TerrainGenerationConfig _config;
        private GenerationRun _resolvedRun;

        public ValidatedPlacementLedgerSource(IPlacementLedgerSource source, string expectedDigest, TreeSurroundSpeciesTable species,
            TerrainGenerationConfig config)
        {
            _ledgerSource = source;
            _expectedPlacementLedgerDigest = expectedDigest;
            _treeSurroundSpecies = species;
            _config = config;
        }

        public GenerationRun Resolve()
        {
            // 台帳(pass-1)は取り逃し時にだけ高々1回解決する。先焼きキャッシュが全部hitなら起動でpass-1を回さない
            // The ledger (pass-1) resolves at most once and only on a miss; when the prebaked cache hits everywhere, startup never runs pass-1
            if (_resolvedRun != null) return _resolvedRun;

            var run = _ledgerSource.Resolve();
            var actualDigest = run.Ledger.ComputeDigest();
            if (actualDigest != _expectedPlacementLedgerDigest)
                throw SurfaceGenerationValidation.Failure(_config, "ledger",
                    $"[TileVisualBaker] Resolved placement ledger digest '{actualDigest}' does not match expected digest '{_expectedPlacementLedgerDigest}'.");

            // 表示高さの手順は台帳と同じ版の生成が決める。版が食い違えば別の地表で焼くことになる
            // The display-height policy comes from generation at the ledger's revision; a mismatch would bake another surface
            if (run.Config.surfaceRevision != _config.surfaceRevision)
                throw SurfaceGenerationValidation.Failure(_config, "ledger",
                    $"[TileVisualBaker] Resolved generation revision '{run.Config.surfaceRevision}' differs from the baker's revision.");

            // 塗る樹種かどうかは塗り側が決めるが、未登録樹種は台帳と樹種表の出所違いなので解決時に一度だけ止める
            // The painter decides which species paint, but an unregistered species means the ledger and table have different sources, so stop once on resolution
            foreach (var placement in run.Ledger.Placements)
            {
                if (placement.SurroundEffect != TerrainSurroundEffectType.treeRootPatch) continue;
                if (_treeSurroundSpecies.IsRegistered(placement.Guid)) continue;

                throw SurfaceGenerationValidation.Failure(_config, "ledger",
                    $"[TileVisualBaker] Ledger placement '{placement.Guid}' carries {nameof(TerrainSurroundEffectType.treeRootPatch)} " +
                    "but is absent from the tree species table; the ledger and the species table came from different biome sets.");
            }

            _resolvedRun = run;
            return _resolvedRun;
        }
    }
}
