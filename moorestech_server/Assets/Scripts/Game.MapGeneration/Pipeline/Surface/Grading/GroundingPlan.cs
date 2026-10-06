using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Grading
{
    public sealed class GroundingPlan
    {
        private readonly SurfaceTileGrid _grid;
        private readonly int[] _bottoms;
        private readonly IReadOnlyList<VeinGroundingPad> _pads;

        internal GroundingPlan(SurfaceTileGrid grid, int[] bottoms,
            IReadOnlyList<VeinGroundingPad> pads)
        {
            _grid = grid;
            _bottoms = bottoms;
            _pads = pads;
        }

        public PlacementLedger Apply(MapGenerationOutput output, PlacementLedger ledger)
        {
            // AABBを移し、見た目台帳には整地面だけを追加する
            // Move AABBs and add only grading pads to the visual ledger
            int index = 0;
            Move(output.ItemVeins);
            Move(output.FluidVeins);
            var positions = new List<Vector3>();
            foreach (var placement in ledger.Placements) positions.Add(placement.ScenePosition);
            var grounded = ledger.WithScenePositions(positions);
            foreach (var pad in _pads) grounded.AddGroundingPad(pad);

            // 全域の一枚の格子から投影し、共有頂点へ複製する
            // Project one global lattice and duplicate its shared vertices to all owners
            var heights = new float[_grid.Geometry.Depth, _grid.Geometry.Width];
            for (int z = 0; z < _grid.Geometry.Depth; z++)
            for (int x = 0; x < _grid.Geometry.Width; x++)
                heights[z, x] = _grid.GetHeight(x, z) / _grid.Config.terrainHeight;
            var projected = GroundingHeightProjector.Apply(heights, _grid.Geometry.Origin,
                _grid.Geometry.Spacing, _grid.Config.terrainHeight, _pads);
            for (int z = 0; z < _grid.Geometry.Depth; z++)
            for (int x = 0; x < _grid.Geometry.Width; x++)
                _grid.SetHeight(x, z, projected[z, x] * _grid.Config.terrainHeight);
            return grounded;

            #region Internal

            void Move(List<PlacedVein> veins)
            {
                for (int i = 0; i < veins.Count; i++)
                {
                    var vein = veins[i];
                    var shift = Vector3Int.up * (_bottoms[index++] - vein.Min.y);
                    veins[i] = new PlacedVein(vein.VeinGuid, vein.Min + shift, vein.Max + shift);
                }
            }

            #endregion
        }
    }
}
