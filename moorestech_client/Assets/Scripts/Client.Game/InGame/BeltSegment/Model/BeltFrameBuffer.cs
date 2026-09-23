using System.Collections.Generic;
using Game.BeltSegment;
namespace Client.Game.InGame.BeltSegment.Model
{
    internal sealed class BeltFrameBuffer
    {
        private const int MaximumFrames = 256;
        private readonly SortedDictionary<(ulong, uint), BeltWorldFrame> frames = new();
        internal bool Add(BeltWorldFrame frame)
        {
            frames[(frame.Position.Tick, frame.Position.Sequence)] = frame;
            if (frames.Count <= MaximumFrames) return true;
            frames.Clear();
            return false;
        }
        internal void DiscardCovered(ulong generation, BeltStreamPosition position)
        {
            var obsolete = new List<(ulong, uint)>();
            foreach (var pair in frames)
                if (pair.Value.Generation < generation || Compare(pair.Value.Position, position) <= 0) obsolete.Add(pair.Key);
            foreach (var key in obsolete) frames.Remove(key);
        }
        internal bool TryTake(ulong generation, BeltStreamPosition position, out BeltWorldFrame frame)
        {
            frame = null;
            foreach (var pair in frames)
            {
                if (pair.Value.Generation != generation || Compare(pair.Value.Previous, position) != 0) continue;
                frame = pair.Value; frames.Remove(pair.Key); return true;
            }
            return false;
        }
        internal bool HasPending => frames.Count != 0;
        internal static int Compare(BeltStreamPosition left, BeltStreamPosition right)
            => left.Tick != right.Tick ? left.Tick.CompareTo(right.Tick) : left.Sequence.CompareTo(right.Sequence);
    }
}
