using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Special;
using Core.Master;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    // 方向 1 つのモードとフィルタ枠（空の枠は EmptyItemId）
    // One direction's mode and filter slots; an empty slot is EmptyItemId
    public readonly struct FilterSplitterDirectionDetail
    {
        public FilterSplitterMode Mode { get; }
        public IReadOnlyList<ItemId> FilterItemIds { get; }

        public FilterSplitterDirectionDetail(FilterSplitterMode mode, IReadOnlyList<ItemId> filterItemIds)
        {
            Mode = mode;
            FilterItemIds = filterItemIds;
        }
    }

    /// <summary>
    /// フィルタ分配器の方向ごとの設定（Web FilterSplitterDto と同じ並び）
    /// Filter splitter per-direction settings, in the same order as Web FilterSplitterDto
    /// </summary>
    public sealed class FilterSplitterDetail
    {
        public IReadOnlyList<FilterSplitterDirectionDetail> Directions { get; }

        public FilterSplitterDetail(IReadOnlyList<FilterSplitterDirectionDetail> directions)
        {
            Directions = directions;
        }
    }
}
