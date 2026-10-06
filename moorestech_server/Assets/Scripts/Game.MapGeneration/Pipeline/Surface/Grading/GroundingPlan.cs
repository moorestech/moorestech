using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Grading
{
    public sealed class GroundingPlan
    {
        private readonly SurfaceTileGrid _grid;
        private readonly SurfacePlacementBindings _bindings;
        private readonly int[] _bottoms;
        private readonly IReadOnlyList<VeinGroundingPad> _pads;

        internal GroundingPlan(SurfaceTileGrid grid, SurfacePlacementBindings bindings, int[] bottoms,
            IReadOnlyList<VeinGroundingPad> pads)
        {
            _grid = grid;
            _bindings = bindings;
            _bottoms = bottoms;
            _pads = pads;
        }

        public PlacementLedger Apply(MapGenerationOutput output, PlacementLedger ledger)
        {
            // AABBと台帳を同じ計画で移し、元台帳は保持する
            // Move AABBs and ledger through one plan while retaining the source ledger
            int index = 0;
            Move(output.ItemVeins);
            Move(output.FluidVeins);
            var grounded = _bindings.ApplyVeinPositions(output, ledger);
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
