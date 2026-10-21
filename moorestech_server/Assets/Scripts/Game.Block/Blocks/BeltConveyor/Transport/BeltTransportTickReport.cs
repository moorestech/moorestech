using Game.Block.Blocks.BeltConveyor.Sync.Diff;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // 搬送tick1回の結果。tick中に成立した機械との搬送差分。組の作り直しはtick末尾で別に通知される
    // The outcome of one transport tick: the machine handoff diff settled during the tick; rebuilds are announced separately at the tick end
    public readonly struct BeltTransportTickReport
    {
        public readonly BeltTickDiff Diff;

        public BeltTransportTickReport(BeltTickDiff diff)
        {
            Diff = diff;
        }
    }
}
