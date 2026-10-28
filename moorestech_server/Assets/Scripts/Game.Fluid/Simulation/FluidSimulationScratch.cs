using Core.Master;

namespace Game.Fluid.Simulation
{
    // Stepper が 1 tick の中だけで使う作業値。網と同じ長さで持ち、網を作り直したときに一緒に作り直す。状態の配列とは分けて、全量送信・ハッシュ・セーブが触らないようにする
    // Per-tick scratch values used only inside the stepper; sized like the network and recreated with it, kept apart from the state arrays so the full-state send, hash and save never touch them
    // ノード列はノード範囲のフェーズが自分の添字だけに書き、面列は面範囲のフェーズが自分の添字だけに書く(範囲を分けて回せる根拠)
    // Node columns are written only by node-range phases at their own index and face columns only by face-range phases, which is what allows ranges to be split
    public sealed class FluidSimulationScratch
    {
        // tick 開始時点の量。流出・流入の比例縮小はこの値を基準にする
        // Amount at the start of the tick; outflow and inflow scaling are based on it
        internal readonly long[] NodeAmountStart;
        internal readonly long[] NodeOutflowSum;
        internal readonly long[] NodeInflowSum;

        // この tick にそのノードが受け入れる流体。中身があればその流体、空なら番号最小の流入面の流体
        // The fluid the node accepts this tick: its own when it holds fluid, else the fluid of its lowest-index incoming face
        internal readonly FluidId[] NodeIncomingFluid;

        internal readonly long[] FaceDesired;
        internal readonly long[] FaceTentative;
        internal readonly long[] FaceSettled;

        public FluidSimulationScratch(FluidSimNetwork network)
        {
            NodeAmountStart = new long[network.NodeCount];
            NodeOutflowSum = new long[network.NodeCount];
            NodeInflowSum = new long[network.NodeCount];
            NodeIncomingFluid = new FluidId[network.NodeCount];
            FaceDesired = new long[network.FaceCount];
            FaceTentative = new long[network.FaceCount];
            FaceSettled = new long[network.FaceCount];
        }
    }
}
