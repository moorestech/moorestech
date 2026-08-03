using System.Collections.Generic;

namespace Core.Inventory
{
    /// <summary>
    ///     インベントリ整理（ソート）の対象から除外するスロットを宣言するインターフェース
    ///     Interface declaring slots that should be excluded from inventory sorting
    /// </summary>
    public interface ISortExcludedSlots
    {
        IReadOnlyCollection<int> SortExcludedSlots { get; }
    }
}
