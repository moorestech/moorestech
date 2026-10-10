using System.Collections.Generic;
using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Sync.Diff
{
    // サーバーで成立した機械との搬送を、次に取り出されるまで溜める。搬入口と機械受け手が書き、tickの束を作る側が搬送tick直後に取り出す
    // Accumulates settled machine handoffs on the server until taken; the inlet and machine receivers write, and the tick-bundle sender takes right after the transport tick
    public sealed class BeltTransportDiffRecorder
    {
        private readonly List<BeltMachineInsertRecord> _inserts = new();
        private readonly List<BeltMachineExtractRecord> _extracts = new();

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
            if (_inserts.Count == 0 && _extracts.Count == 0) return BeltTickDiff.Empty;
            var diff = new BeltTickDiff(_inserts.ToArray(), _extracts.ToArray());
            _inserts.Clear();
            _extracts.Clear();
            return diff;
        }

        // 再構築で番号が付け直されるので、旧構成の番号で書かれた未取り出し分は捨てる。中身は再構築後の全量に含まれる
        // A rebuild renumbers segments, so untaken records written with old numbers are dropped; their effect is already in the post-rebuild full state
        public void Discard()
        {
            _inserts.Clear();
            _extracts.Clear();
        }
    }
}
