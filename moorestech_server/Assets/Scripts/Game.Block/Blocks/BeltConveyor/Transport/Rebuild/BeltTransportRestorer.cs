using System.Collections.Generic;
using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Transport.Rebuild
{
    // 旧構成から取り出したアイテムを、撤去と再構築の仕様の優先順で新構成へ復元する。置けないアイテムは消滅する
    // Restores items captured from the old assembly into the new one in the priority order of the removal/rebuild spec; items that cannot be placed vanish
    public static class BeltTransportRestorer
    {
        public static void Restore(BeltTransportSnapshot snapshot, BeltTransportAssembly assembly)
        {
            var locator = assembly.Locator;
            var placements = new BeltSegmentPlacement[assembly.Segments.Length];
            for (var i = 0; i < placements.Length; i++) placements[i] = new BeltSegmentPlacement();
            var corruptedSegments = CollectCorruptedSegments();

            // 走行中→消えるbuffer→内部segmentの順に確定し、最後にsegmentごとの配置をCoreへ書く
            // Settle running items, then vanishing buffers, then internal segments, and finally write each segment's placement into Core
            PlaceRunningItems();
            PlaceBufferItems();
            RestoreInternalItems();
            for (var i = 0; i < placements.Length; i++) assembly.Segments[i].RestoreItems(placements[i].ToStates());

            #region Internal

            HashSet<int> CollectCorruptedSegments()
            {
                // 読めない保存内容を持つblockが属するsegmentには、何も置かない
                // Nothing is placed into a segment that contains a block with unreadable saved content
                var segments = new HashSet<int>();
                foreach (var block in snapshot.CorruptedBlocks)
                    if (locator.TryLocate(block, out var location)) segments.Add(location.SegmentIndex);
                return segments;
            }

            void PlaceRunningItems()
            {
                // 先頭のマスが残るものだけが候補。所属マスとマス内進行量を保ち、経路が変わるものは表示用の進入方向を合わせる
                // Only items whose head cell survives are candidates; cell and in-cell progress are kept, and items whose path changed get their entry direction aligned
                var candidates = new List<BeltRestoreCandidate>();
                foreach (var record in snapshot.RunningItems)
                {
                    if (!locator.TryLocate(record.CellBlockInstanceId, out var location) || corruptedSegments.Contains(location.SegmentIndex)) continue;
                    var matches = BeltCellLocator.MatchesPath(location.Cell, record.Item.EntryDirection);
                    var item = matches ? record.Item : record.Item.WithEntryDirection(BeltCellLocator.AlignToPath(location.Cell, record.Item.EntryDirection));
                    var distance = location.CellExitDistance + record.DistanceToCellExit;
                    candidates.Add(new BeltRestoreCandidate(matches ? 1 : 2, location.SegmentIndex, distance, item, record.SourceSegmentIndex, record.SourceOrder));
                }
                candidates.Sort();
                foreach (var candidate in candidates) placements[candidate.SegmentIndex].TryPlace(candidate.DistanceToExit, candidate.Item);
            }

            void PlaceBufferItems()
            {
                // 存続するbufferへはそのまま引き継ぐ。消えるbufferのアイテムは、そのマスが丸ごと空いているときだけ出口に先頭を置く
                // A surviving buffer takes its item back as is; a vanishing buffer's item is placed with its head at the cell exit only when that whole cell is free
                var candidates = new List<BeltRestoreCandidate>();
                foreach (var record in snapshot.BufferItems)
                {
                    if (!locator.TryLocate(record.CellBlockInstanceId, out var location) || corruptedSegments.Contains(location.SegmentIndex)) continue;
                    if (location.IsLastCell && assembly.Segments[location.SegmentIndex] is BeltBufferedSegment buffered)
                    {
                        buffered.Buffer.RestoreItem(record.Item);
                        continue;
                    }
                    var matches = BeltCellLocator.MatchesPath(location.Cell, record.Item.EntryDirection);
                    var item = matches ? record.Item : record.Item.WithEntryDirection(BeltCellLocator.AlignToPath(location.Cell, record.Item.EntryDirection));
                    candidates.Add(new BeltRestoreCandidate(3, location.SegmentIndex, location.CellExitDistance, item, record.SourceSegmentIndex, 0));
                }
                candidates.Sort();
                foreach (var candidate in candidates) placements[candidate.SegmentIndex].TryPlace(candidate.DistanceToExit, candidate.Item);
            }

            void RestoreInternalItems()
            {
                // 同じ合流マス・同じ入力方向に内部segmentが再び作られる場合だけ、出口までの距離を保って引き継ぐ
                // Carried over with their distances only when an internal segment is created again for the same merge cell and input direction
                foreach (var record in snapshot.InternalItems)
                {
                    if (snapshot.CorruptedBlocks.Contains(record.MergeBlockInstanceId)) continue;
                    if (locator.TryFindInternal(record.MergeBlockInstanceId, record.InputDirection, out var segmentIndex))
                        assembly.Segments[segmentIndex].RestoreItems(record.States);
                }
            }

            #endregion
        }
    }
}
