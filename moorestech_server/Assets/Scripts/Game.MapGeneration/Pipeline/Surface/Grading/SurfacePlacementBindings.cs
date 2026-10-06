using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Grading
{
    public sealed class SurfacePlacementBindings
    {
        private readonly Dictionary<int, int> _items = new();
        private readonly Dictionary<int, int> _fluids = new();
        private readonly Dictionary<int, int> _objects = new();
        private readonly HashSet<int> _ledgerIndices = new();
        private readonly Dictionary<int, int> _originalBottoms = new();

        public void AddItemVein(int outputIndex, int ledgerIndex)
        {
            Add(_items, outputIndex, ledgerIndex);
        }

        public void AddFluidVein(int outputIndex, int ledgerIndex)
        {
            Add(_fluids, outputIndex, ledgerIndex);
        }

        public void AddMapObject(int outputIndex, int ledgerIndex)
        {
            Add(_objects, outputIndex, ledgerIndex);
        }

        internal void CaptureVeinBottoms(MapGenerationOutput output)
        {
            // 対応は確定append時のindexで保持し、丸め位置から逆引きしない
            // Retain confirmed append indices rather than matching rounded positions
            _originalBottoms.Clear();
            foreach (var pair in _items) _originalBottoms.Add(pair.Value, output.ItemVeins[pair.Key].Min.y);
            foreach (var pair in _fluids) _originalBottoms.Add(pair.Value, output.FluidVeins[pair.Key].Min.y);
        }

        public PlacementLedger ApplyVeinPositions(MapGenerationOutput output, PlacementLedger ledger)
        {
            var positions = CopyPositions(ledger);
            Shift(_items, output.ItemVeins);
            Shift(_fluids, output.FluidVeins);
            return ledger.WithScenePositions(positions);

            #region Internal

            void Shift(Dictionary<int, int> bindings, List<PlacedVein> veins)
            {
                // 元配置の小数YとXZを残し、AABB移動量だけ加える
                // Preserve original fractional Y and XZ while adding only the AABB shift
                foreach (var pair in bindings)
                    positions[pair.Value] += Vector3.up * (veins[pair.Key].Min.y - _originalBottoms[pair.Value]);
            }

            #endregion
        }

        public PlacementLedger ApplyMapObjectPositions(MapGenerationOutput output, PlacementLedger ledger)
        {
            var positions = CopyPositions(ledger);
            foreach (var pair in _objects)
            {
                var position = positions[pair.Value];
                position.y = output.MapObjects[pair.Key].Position.y;
                positions[pair.Value] = position;
            }
            return ledger.WithScenePositions(positions);
        }

        private static List<Vector3> CopyPositions(PlacementLedger ledger)
        {
            var positions = new List<Vector3>(ledger.Placements.Count);
            foreach (var entry in ledger.Placements) positions.Add(entry.ScenePosition);
            return positions;
        }

        private void Add(Dictionary<int, int> target, int outputIndex, int ledgerIndex)
        {
            if (outputIndex < 0 || ledgerIndex < 0 || target.ContainsKey(outputIndex) || !_ledgerIndices.Add(ledgerIndex))
            {
                string reason = $"Invalid or duplicate surface binding: output={outputIndex}, ledger={ledgerIndex}.";
                Debug.LogError(reason);
                throw new InvalidOperationException(reason);
            }
            target.Add(outputIndex, ledgerIndex);
        }
    }
}
