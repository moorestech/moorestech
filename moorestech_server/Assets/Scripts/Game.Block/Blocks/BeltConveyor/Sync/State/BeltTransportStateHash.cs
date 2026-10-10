using Core.BeltTransport;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Sync.State
{
    // 全量の決定的なハッシュ(FNV-1a、列車のTrainUnitSnapshotHashCalculatorと同型)。segment番号順に全項目を混ぜる
    // Deterministic hash of the full state (FNV-1a, same shape as the train's TrainUnitSnapshotHashCalculator); mixes every field in segment-number order
    public static class BeltTransportStateHash
    {
        private const uint FnvOffset = 2166136261;
        private const uint FnvPrime = 16777619;

        public static uint Compute(BeltTransportFullState fullState)
        {
            var hash = Mix(FnvOffset, fullState.Segments.Length);
            for (var i = 0; i < fullState.Segments.Length; i++)
            {
                hash = Mix(hash, i);
                hash = MixShape(hash, fullState.Segments[i].Shape);
                hash = MixContents(hash, fullState.Segments[i]);
            }
            return hash;
        }

        private static uint MixShape(uint current, BeltSegmentShape shape)
        {
            var hash = Mix(current, (int)shape.Kind);
            hash = Mix(hash, shape.IsInternal ? 1 : 0);
            hash = Mix(hash, shape.Speed);
            hash = Mix(hash, (int)shape.Forward);
            hash = Mix(hash, shape.Cells.Length);
            foreach (var cell in shape.Cells)
            {
                hash = MixPosition(hash, cell.Position);
                hash = Mix(hash, (int)cell.Forward);
            }
            hash = MixLinks(hash, shape.Inputs);
            hash = MixLinks(hash, shape.Outputs);
            return hash;
        }

        private static uint MixContents(uint current, BeltSegmentState state)
        {
            var hash = Mix(current, state.PriorityOrder);
            hash = Mix(hash, state.Items.Length);
            foreach (var item in state.Items) hash = MixItem(hash, item);
            hash = Mix(hash, state.HasBufferItem ? 1 : 0);
            if (state.HasBufferItem) hash = MixItem(hash, state.BufferItem);
            return hash;
        }

        private static uint MixLinks(uint current, BeltLinkShape[] links)
        {
            var hash = Mix(current, links.Length);
            foreach (var link in links)
            {
                hash = Mix(hash, (int)link.Direction);
                hash = Mix(hash, (int)link.EntryDirection);
                hash = Mix(hash, link.PartnerSegmentIndex);
            }
            return hash;
        }

        private static uint MixItem(uint current, in BeltItemSnapshot item)
        {
            var hash = Mix(current, item.ItemId.AsPrimitive());
            hash = MixLong(hash, item.ItemInstanceId.AsPrimitive());
            hash = Mix(hash, (int)item.EntryDirection);
            hash = Mix(hash, item.DistanceToExit);
            return hash;
        }

        private static uint MixPosition(uint current, Vector3Int position)
        {
            var hash = Mix(current, position.x);
            hash = Mix(hash, position.y);
            hash = Mix(hash, position.z);
            return hash;
        }

        private static uint MixLong(uint current, long value)
        {
            unchecked
            {
                var hash = Mix(current, (int)value);
                return Mix(hash, (int)(value >> 32));
            }
        }

        private static uint Mix(uint current, int value)
        {
            unchecked
            {
                return (current ^ (uint)value) * FnvPrime;
            }
        }
    }
}
