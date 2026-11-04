using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps.V9ToV10
{
    /// <summary>
    /// 1つの占有空間（world 全体か、ブループリント1つ）のブロックを寝かせる。水平のものを先に占有へ入れ、縦のものを渡された順に寝かせ、重なるものを撤去対象として返す
    /// Lays flat the blocks of one occupancy space (the whole world, or one blueprint); horizontal ones claim cells first, vertical ones are laid flat in the given order, and the overlapping ones are returned for removal
    /// </summary>
    internal static class VerticalBlockLayFlattener
    {
        // entries は重なり時に残す優先順（world は instanceId 昇順、ブループリントは添字順）に並べて渡す
        // Pass entries in keep-priority order for overlaps (instanceId ascending for world, index order for blueprints)
        public static LayFlatOutcome LayFlat(IReadOnlyList<LegacyBlockEntry> entries)
        {
            var outcome = new LayFlatOutcome();

            // 向きを読替え、縦パネルは壁パネルへ
            // Renumber directions; vertical panels become wall panels
            foreach (var entry in entries) entry.Rewrite();

            // 縦が無ければ重なりは起きないので、マスタのサイズを引かずに終える
            // Without vertical blocks no overlap can occur, so finish without looking up master sizes
            if (!entries.Any(entry => entry.WasVertical)) return outcome;

            // 水平だったブロックは位置も占有も変わらないので先に登録する
            // Blocks that were horizontal keep their place and footprint, so they claim cells first
            var occupancy = new HashSet<Vector3Int>();
            foreach (var entry in entries.Where(entry => !entry.WasVertical))
            {
                var size = entry.LookUpMasterBlockSize();
                if (size != null) occupancy.UnionWith(LegacyTwelveDirection.OccupiedCells(entry.MinCorner, entry.Horizontal, size.Value));
            }

            // 縦を順に寝かせ、重なりは撤去へ
            // Lay vertical blocks flat in order; overlaps go to removal
            foreach (var entry in entries.Where(entry => entry.WasVertical))
            {
                var size = entry.LookUpMasterBlockSize();
                if (size == null)
                {
                    outcome.CountLaidFlat(entry);
                    continue;
                }

                var cells = LegacyTwelveDirection.OccupiedCells(entry.MinCorner, entry.Horizontal, size.Value).ToList();
                if (occupancy.Overlaps(cells))
                {
                    outcome.Removed.Add(entry);
                    continue;
                }

                occupancy.UnionWith(cells);
                outcome.CountLaidFlat(entry);
            }

            return outcome;
        }
    }

    /// <summary>
    /// 寝かせ結果（撤去対象・残件数）
    /// Lay-flat result: removal targets and remaining counts
    /// </summary>
    internal sealed class LayFlatOutcome
    {
        public readonly List<LegacyBlockEntry> Removed = new();
        public int LaidFlatCount { get; private set; }
        public int WallPanelCount { get; private set; }

        public void CountLaidFlat(LegacyBlockEntry entry)
        {
            LaidFlatCount++;
            if (entry.ReplacesWithWallPanel) WallPanelCount++;
        }
    }
}
