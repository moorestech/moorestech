namespace Game.Fluid.Simulation
{
    // Stepper が 1 tick の中だけで使う作業値。網と同じ長さで持ち、網を作り直したときに一緒に作り直す。状態の配列とは分けて、全量送信・ハッシュ・セーブが触らないようにする
    // Per-tick scratch values used only inside the stepper; sized like the network and recreated with it, kept apart from the state arrays so the full-state send, hash and save never touch them
    public sealed class FluidSimulationScratch
    {
        internal readonly double[] OutflowSum;
        internal readonly double[] InflowSum;
        internal readonly double[] OutflowScale;
        internal readonly double[] InflowScale;
        internal readonly double[] TentativeFlux;

        public FluidSimulationScratch(FluidSimNetwork network)
        {
            OutflowSum = new double[network.NodeCount];
            InflowSum = new double[network.NodeCount];
            OutflowScale = new double[network.NodeCount];
            InflowScale = new double[network.NodeCount];
            TentativeFlux = new double[network.FaceCount];
        }
    }
}
