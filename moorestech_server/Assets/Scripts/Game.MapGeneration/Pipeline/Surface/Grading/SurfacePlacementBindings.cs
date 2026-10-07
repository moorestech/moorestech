using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Grading
{
    internal sealed class SurfacePlacementBindings
    {
        private readonly TerrainGenerationConfig _config;
        private readonly Dictionary<int, int> _objects = new();
        private readonly HashSet<int> _ledgerIndices = new();

        public SurfacePlacementBindings(TerrainGenerationConfig config)
        {
            _config = config;
        }

        public void AddMapObject(int outputIndex, int ledgerIndex)
        {
            if (outputIndex < 0 || ledgerIndex < 0 || _objects.ContainsKey(outputIndex) || !_ledgerIndices.Add(ledgerIndex))
                throw SurfaceGenerationValidation.Failure(_config, "bindings",
                    $"Invalid or duplicate surface binding: output={outputIndex}, ledger={ledgerIndex}.");
            _objects.Add(outputIndex, ledgerIndex);
        }

        public PlacementLedger ApplyMapObjectPositions(MapGenerationOutput output, PlacementLedger ledger)
        {
            var positions = new List<Vector3>(ledger.Placements.Count);
            foreach (var entry in ledger.Placements) positions.Add(entry.ScenePosition);
            foreach (var pair in _objects)
            {
                var position = positions[pair.Value];
                position.y = output.MapObjects[pair.Key].Position.y;
                positions[pair.Value] = position;
            }
            return ledger.WithScenePositions(positions, _config);
        }
    }
}
