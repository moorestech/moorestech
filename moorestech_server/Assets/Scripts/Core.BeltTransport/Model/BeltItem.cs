// MyBeltConvSegmentのCPU実装をムアステ向けに変更。
// Adapted from MyBeltConvSegment CPU implementation; see LICENSE.txt.
using System;

namespace Core.BeltTransport
{
    public readonly struct BeltItem
    {
        public readonly Guid Guid;
        public readonly int ItemId;

        public BeltItem(Guid guid, int itemId)
        {
            Guid = guid;
            ItemId = itemId;
        }
    }
}
