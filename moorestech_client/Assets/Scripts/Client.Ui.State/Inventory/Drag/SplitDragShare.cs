using System.Collections.Generic;
using Client.Ui.State.Inventory.Actions;

namespace Client.Ui.State.Inventory.Drag
{
    /// <summary>
    /// 分割ドラッグの等分の結果（行先・1 か所あたりの個数・古くて除いた番地の数）
    /// Split-drag share result (destinations, count per slot, number of stale refs dropped)
    /// </summary>
    public sealed class SplitDragShare
    {
        public IReadOnlyList<PlayerSlotRef> Destinations { get; }
        public int CountPerSlot { get; }
        public int IgnoredStaleCount { get; }

        public SplitDragShare(IReadOnlyList<PlayerSlotRef> destinations, int countPerSlot, int ignoredStaleCount)
        {
            Destinations = destinations;
            CountPerSlot = countPerSlot;
            IgnoredStaleCount = ignoredStaleCount;
        }
    }
}
