using ClassLibrary;
using Core.BeltTransport;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Sync.State
{
    // 全量の決定的なハッシュ。segment ごとに独立したハッシュ(番号・形・中身)を作り、順序に依存しない加算で畳む(ElementHash。列車・流体と同じ部品)
    // Deterministic hash of the full state: each segment gets an independent hash (index, shape, contents) folded by order-independent addition (ElementHash, shared with train and fluid)
    public static class BeltTransportStateHash
    {
        private const uint SegmentSeed = 0x7365676D;

        public static uint Compute(BeltTransportFullState fullState)
        {
            unchecked
            {
                var sum = ElementHash.Count(SegmentSeed, fullState.Segments.Length);
                for (var i = 0; i < fullState.Segments.Length; i++)
                {
                    var hash = ElementHash.Begin(SegmentSeed, i);
                    hash = AddShape(hash, fullState.Segments[i].Shape);
                    hash = AddContents(hash, fullState.Segments[i]);
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

            uint AddContents(uint current, BeltSegmentState state)
            {
                var hash = ElementHash.Add(current, state.PriorityOrder);
                hash = ElementHash.Add(hash, state.CurrentSpeed);
                hash = ElementHash.Add(hash, state.Items.Length);
                foreach (var item in state.Items) hash = AddItem(hash, item);
                hash = ElementHash.Add(hash, state.HasBufferItem);
                if (state.HasBufferItem) hash = AddItem(hash, state.BufferItem);
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

            uint AddItem(uint current, in BeltItemSnapshot item)
            {
                var hash = ElementHash.Add(current, item.ItemId.AsPrimitive());
                hash = ElementHash.Add(hash, item.ItemInstanceId.AsPrimitive());
                hash = ElementHash.Add(hash, (int)item.EntryDirection);
                hash = ElementHash.Add(hash, item.DistanceToExit);
                return hash;
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
