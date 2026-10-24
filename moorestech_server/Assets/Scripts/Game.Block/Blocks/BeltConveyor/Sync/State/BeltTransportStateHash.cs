using ClassLibrary;
using Core.BeltTransport;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Sync.State
{
    // 組の決定的なハッシュ。segment ごとに独立したハッシュ(番号・形・中身)を作り、順序に依存しない加算で畳む(ElementHash。列車・流体と同じ部品)
    // Deterministic hash of the assembly: each segment gets an independent hash (index, shape, contents) folded by order-independent addition (ElementHash, shared with train and fluid)
    // 形の配列と Core segment の配列を直接なめる。全量の切り出しも、アイテム列の配列への写しも無い(割り当て0)
    // Walks the shape array and the Core segment array directly; no full-state capture and no copy of the item run into an array (zero allocation)
    public static class BeltTransportStateHash
    {
        private const uint SegmentSeed = 0x7365676D;

        public static uint Compute(BeltSegmentShape[] shapes, BeltConveyorSegment[] segments)
        {
            unchecked
            {
                var sum = ElementHash.Count(SegmentSeed, segments.Length);
                for (var i = 0; i < segments.Length; i++)
                {
                    var hash = ElementHash.Begin(SegmentSeed, i);
                    hash = AddShape(hash, shapes[i]);
                    hash = AddContents(hash, segments[i]);
                    sum += ElementHash.Finish(hash);
                }
                return sum;
            }

            #region Internal

            uint AddShape(uint current, BeltSegmentShape shape)
            {
                var hash = ElementHash.Add(current, (int)shape.Kind);
                hash = ElementHash.Add(hash, shape.IsInternal);
                hash = ElementHash.Add(hash, shape.Capacity);
                hash = ElementHash.Add(hash, shape.Speed);
                hash = ElementHash.Add(hash, (int)shape.Forward);
                hash = ElementHash.Add(hash, shape.Cells.Length);
                foreach (var cell in shape.Cells)
                {
                    hash = AddPosition(hash, cell.Position);
                    hash = ElementHash.Add(hash, (int)cell.Forward);
                }
                hash = AddLinks(hash, shape.Inputs);
                hash = AddLinks(hash, shape.Outputs);
                return hash;
            }

            uint AddContents(uint current, BeltConveyorSegment segment)
            {
                // 走行中は出口に近い順。bufferは合流・分岐だけが持ち、距離は0
                // Running items in exit order; only merges and branches hold a buffer, whose item sits at distance 0
                var hash = ElementHash.Add(current, segment.PriorityOrder);
                hash = ElementHash.Add(hash, segment.Speed);
                hash = ElementHash.Add(hash, segment.Count);
                foreach (var state in segment.EnumerateItems()) hash = AddItem(hash, state.Item, state.DistanceToExit);

                var bufferItem = default(BeltItem);
                var hasBufferItem = segment is BeltBufferedSegment buffered && buffered.Buffer.TryGetItem(out bufferItem);
                hash = ElementHash.Add(hash, hasBufferItem);
                if (hasBufferItem) hash = AddItem(hash, bufferItem, 0);
                return hash;
            }

            uint AddLinks(uint current, BeltLinkShape[] links)
            {
                var hash = ElementHash.Add(current, links.Length);
                foreach (var link in links)
                {
                    hash = ElementHash.Add(hash, (int)link.Direction);
                    hash = ElementHash.Add(hash, (int)link.EntryDirection);
                    hash = ElementHash.Add(hash, link.PartnerSegmentIndex);
                }
                return hash;
            }

            uint AddItem(uint current, in BeltItem item, int distanceToExit)
            {
                var hash = ElementHash.Add(current, item.ItemId.AsPrimitive());
                hash = ElementHash.Add(hash, item.ItemInstanceId.AsPrimitive());
                hash = ElementHash.Add(hash, (int)item.EntryDirection);
                return ElementHash.Add(hash, distanceToExit);
            }

            uint AddPosition(uint current, Vector3Int position)
            {
                var hash = ElementHash.Add(current, position.x);
                hash = ElementHash.Add(hash, position.y);
                return ElementHash.Add(hash, position.z);
            }

            #endregion
        }
    }
}
