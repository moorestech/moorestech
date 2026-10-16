using Game.Block.Blocks.BeltConveyor.Sync.Diff;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // 搬送tick1回の結果。そのtickの先頭で組が作り直されたかと、tick中に成立した機械との搬送差分
    // The outcome of one transport tick: whether the assembly was rebuilt at the tick head, and the machine handoff diff settled during the tick
    public readonly struct BeltTransportTickReport
    {
        // 作り直されたtickは差分でなく搬送後の全量を送る。差分の効果は全量に含まれている
        // A rebuilt tick sends the post-tick full state instead of the diff; the diff's effect is already in that full state
        public readonly bool WasRebuilt;
        public readonly BeltTickDiff Diff;

        public BeltTransportTickReport(bool wasRebuilt, BeltTickDiff diff)
        {
            WasRebuilt = wasRebuilt;
            Diff = diff;
        }
    }
}
