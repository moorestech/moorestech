using System.Collections.Generic;
using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Transport.Rebuild
{
    // 新しいsegment1本に配置済みのアイテムを出口に近い順で持ち、重なる候補を退ける
    // Items already placed into one new segment in exit order, rejecting candidates that overlap them
    public sealed class BeltSegmentPlacement
    {
        private readonly List<int> _distances = new();
        private readonly List<BeltItem> _items = new();

        // 先頭からアイテム幅の範囲が配置済みのどれとも重ならなければ置く。segmentの入口からはみ出す胴体は咎めない
        // Place when the item-width span from the head overlaps no placed item; a body sticking out of the entrance is not rejected
        public bool TryPlace(int distanceToExit, in BeltItem item)
        {
            var index = _distances.BinarySearch(distanceToExit);
            if (index < 0) index = ~index;
            if (0 < index && distanceToExit - _distances[index - 1] < BeltConstants.ItemWidth) return false;
            if (index < _distances.Count && _distances[index] - distanceToExit < BeltConstants.ItemWidth) return false;
            _distances.Insert(index, distanceToExit);
            _items.Insert(index, item);
            return true;
        }

        public BeltItemState[] ToStates()
        {
            var states = new BeltItemState[_distances.Count];
            for (var i = 0; i < states.Length; i++) states[i] = new BeltItemState(_items[i], _distances[i]);
            return states;
        }
    }
}
