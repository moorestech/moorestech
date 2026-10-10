using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Interface;

namespace Game.Block.Blocks.BeltConveyor.Transport.Rebuild
{
    // 新構成から、blockがどのsegmentのどのマスになったかと、合流block＋入力方向の内部segmentを引く
    // Looks up, in the new assembly, which segment and cell a block became, and the internal segment of a merge block plus input direction
    public sealed class BeltCellLocator
    {
        private readonly Dictionary<BlockInstanceId, BeltCellLocation> _locationByBlock = new();
        private readonly Dictionary<(BlockInstanceId, BeltDirection), int> _internalIndexByInput = new();

        public BeltCellLocator(BeltSegmentLayout[] layouts)
        {
            foreach (var layout in layouts)
            {
                if (layout.IsInternal)
                {
                    var merge = layouts[layout.Outputs[0].PartnerSegmentIndex];
                    _internalIndexByInput.Add((merge.Cells[0].BlockInstanceId, layout.Inputs[0].Direction), layout.Index);
                    continue;
                }
                var cells = layout.Cells;
                for (var i = 0; i < cells.Length; i++)
                {
                    var cellExitDistance = (cells.Length - 1 - i) * BeltConstants.ItemWidth;
                    _locationByBlock.Add(cells[i].BlockInstanceId, new BeltCellLocation(layout.Index, cells[i], cellExitDistance, i == cells.Length - 1));
                }
            }
        }

        public bool TryLocate(BlockInstanceId cellBlockInstanceId, out BeltCellLocation location)
        {
            return _locationByBlock.TryGetValue(cellBlockInstanceId, out location);
        }

        public bool TryFindInternal(BlockInstanceId mergeBlockInstanceId, BeltDirection inputDirection, out int segmentIndex)
        {
            return _internalIndexByInput.TryGetValue((mergeBlockInstanceId, inputDirection), out segmentIndex);
        }

        // 進入方向が新しい経路と一致するか。マスの入力接続のどれかが同じ進入方向なら一致
        // Whether the entry direction matches the new path: true when any input connection of the cell has the same entry direction
        public static bool MatchesPath(BeltTopologyCell cell, BeltEntryDirection entryDirection)
        {
            foreach (var input in cell.Inputs)
                if (input.EntryDirection == entryDirection) return true;
            return false;
        }

        // 一致しないアイテムの表示用進入方向を新しい経路へ合わせる。直進の入力を優先し、無ければ方向順の先頭。入力が無ければ元のまま
        // Align a mismatched item's display entry direction to the new path: prefer the straight input, else the first by direction; keep it when there is no input
        public static BeltEntryDirection AlignToPath(BeltTopologyCell cell, BeltEntryDirection entryDirection)
        {
            if (cell.Inputs.Length == 0) return entryDirection;
            var straight = BeltDirections.Opposite(cell.Forward);
            foreach (var input in cell.Inputs)
                if (input.Direction == straight) return input.EntryDirection;
            return cell.Inputs[0].EntryDirection;
        }
    }
}
