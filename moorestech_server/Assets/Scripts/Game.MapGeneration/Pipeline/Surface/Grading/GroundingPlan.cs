using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Grading
{
    internal sealed class GroundingPlan
    {
        private readonly SurfaceTileGrid _grid;
        private readonly IReadOnlyList<VeinGrounding> _groundings;

        internal GroundingPlan(SurfaceTileGrid grid, IReadOnlyList<VeinGrounding> groundings)
        {
            _grid = grid;
            _groundings = groundings;
        }

        public PlacementLedger Apply(PlacementLedger ledger)
        {
            // AABBを移し台帳へ整地面のみ追加
            // Move AABBs and add only pads to the ledger
            var output = _grid.Output;
            if (output.ItemVeins.Count + output.FluidVeins.Count != _groundings.Count)
                throw SurfaceGenerationValidation.Failure(_grid.Config, "grading", "Vein count changed between planning and applying.");
            int index = 0;
            Move(output.ItemVeins);
            Move(output.FluidVeins);
            var pads = new List<VeinGroundingPad>(_groundings.Count);
            foreach (var grounding in _groundings) pads.Add(grounding.Pad);
            var grounded = ledger.WithGroundingPads(pads, _grid.Config);

            // 全域一枚の格子から投影し頂点へ複製
            // Project from one global lattice and copy to shared vertices
            var heights = new float[_grid.Geometry.Depth, _grid.Geometry.Width];
            for (int z = 0; z < _grid.Geometry.Depth; z++)
            for (int x = 0; x < _grid.Geometry.Width; x++)
                heights[z, x] = _grid.GetHeight(x, z) / _grid.Config.terrainHeight;
            // skirtは保存高さへこの1回だけ焼き、表示段はcoreだけを再代入する
            // Skirts are baked into the stored heights this once; the display stage reassigns cores alone
            var skirted = GroundingHeightProjector.ApplySkirts(heights, _grid.Geometry.Origin,
                _grid.Geometry.Spacing, _grid.Config.terrainHeight, pads);
            var projected = GroundingHeightProjector.ApplyCores(skirted, _grid.Geometry.Origin,
                _grid.Geometry.Spacing, _grid.Config.terrainHeight, pads);
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
                    var grounding = _groundings[index++];
                    if (vein.VeinGuid != grounding.Vein.VeinGuid || vein.Min != grounding.Vein.Min || vein.Max != grounding.Vein.Max)
                        throw SurfaceGenerationValidation.Failure(_grid.Config, "grading", $"Vein changed between planning and applying: {vein.VeinGuid} at {vein.Min}.");
                    var shift = Vector3Int.up * (grounding.Bottom - vein.Min.y);
                    veins[i] = new PlacedVein(vein.VeinGuid, vein.Min + shift, vein.Max + shift);
                }
            }

            #endregion
        }
    }
}
