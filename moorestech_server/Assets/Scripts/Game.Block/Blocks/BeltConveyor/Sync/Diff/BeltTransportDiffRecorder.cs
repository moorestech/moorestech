using System.Collections.Generic;
using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Sync.Diff
{
    // サーバーで成立した機械との搬送を、次に取り出されるまで溜める。搬入口と機械受け手が書き、tickの束を作る側が搬送tick直後に取り出す
    // Accumulates settled machine handoffs on the server until taken; the inlet and machine receivers write, and the tick-bundle sender takes right after the transport tick
    public sealed class BeltTransportDiffRecorder
    {
        private readonly List<BeltSegmentSpeedRecord> _speedChanges = new();
        private readonly List<BeltMachineInsertRecord> _inserts = new();
        private readonly List<BeltMachineExtractRecord> _extracts = new();

        public void RecordSpeed(int segmentIndex, int speed)
        {
            _speedChanges.Add(new BeltSegmentSpeedRecord(segmentIndex, speed));
        }

        public void RecordInsert(int segmentIndex, BeltDirection inputDirection, in BeltItem item)
        {
            _inserts.Add(new BeltMachineInsertRecord(segmentIndex, inputDirection, item));
        }

        public void RecordExtract(int segmentIndex, BeltDirection outputDirection)
        {
            _extracts.Add(new BeltMachineExtractRecord(segmentIndex, outputDirection));
        }

        // 溜まった分を1tickの差分として取り出し、空にする
        // Take the accumulated records as one tick's diff and clear
        public BeltTickDiff TakeTickDiff()
        {
            if (_speedChanges.Count == 0 && _inserts.Count == 0 && _extracts.Count == 0) return BeltTickDiff.Empty;
            var diff = new BeltTickDiff(_speedChanges.ToArray(), _inserts.ToArray(), _extracts.ToArray());
            _speedChanges.Clear();
            _inserts.Clear();
            _extracts.Clear();
            return diff;
        }
    }
}
