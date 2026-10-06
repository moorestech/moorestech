using System;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Surround;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Source
{
    public sealed class ValidatedPlacementLedgerSource : IPlacementLedgerSource
    {
        private readonly IPlacementLedgerSource _ledgerSource;
        private readonly string _expectedPlacementLedgerDigest;
        private readonly TreeSurroundSpeciesTable _treeSurroundSpecies;
        private PlacementLedger _resolvedLedger;

        public ValidatedPlacementLedgerSource(IPlacementLedgerSource source, string expectedDigest, TreeSurroundSpeciesTable species)
        {
            _ledgerSource = source;
            _expectedPlacementLedgerDigest = expectedDigest;
            _treeSurroundSpecies = species;
        }

        public PlacementLedger Resolve()
        {
            if (_resolvedLedger != null) return _resolvedLedger;

            var ledger = _ledgerSource.Resolve();
            var actualDigest = ledger.ComputeDigest();
            if (actualDigest != _expectedPlacementLedgerDigest)
                throw Failure(
                    $"[TileVisualBaker] Resolved placement ledger digest '{actualDigest}' does not match expected digest '{_expectedPlacementLedgerDigest}'.");

            // 塗る樹種かどうかは塗り側が決めるが、未登録樹種は台帳と樹種表の出所違いなので解決時に一度だけ止める
            // The painter decides which species paint, but an unregistered species means the ledger and table have different sources, so stop once on resolution
            foreach (var placement in ledger.Placements)
            {
                if (placement.SurroundEffect != TerrainSurroundEffectType.treeRootPatch) continue;
                if (_treeSurroundSpecies.IsRegistered(placement.Guid)) continue;

                throw Failure(
                    $"[TileVisualBaker] Ledger placement '{placement.Guid}' carries {nameof(TerrainSurroundEffectType.treeRootPatch)} " +
                    "but is absent from the tree species table; the ledger and the species table came from different biome sets.");
            }

            _resolvedLedger = ledger;
            return _resolvedLedger;
        }

        private static InvalidOperationException Failure(string reason)
        {
            Debug.LogError(reason);
            return new InvalidOperationException(reason);
        }
    }
}
