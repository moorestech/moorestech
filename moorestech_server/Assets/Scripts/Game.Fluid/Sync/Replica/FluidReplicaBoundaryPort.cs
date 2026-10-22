using System;
using Game.Fluid.Simulation;

namespace Game.Fluid.Sync.Replica
{
    // 複製での境界ポート。機械の受け入れ可否はサーバーが確定した差分で与えるので、予告された量だけを受け取り、残りは返す
    // A boundary port in the replica; the machine's acceptance is dictated by the server's settled diff, so it takes only the announced amount and returns the rest
    public sealed class FluidReplicaBoundaryPort : IFluidBoundaryPort
    {
        public FluidSimNode PipeNode { get; }
        public double FlowCapacityPerTick { get; }
        public double Velocity { get; set; }

        // このtickにサーバーが渡した量。差分の再生が置き、Deliverが消費する
        // The amount the server delivered this tick; set by the diff replay and consumed by Deliver
        private double _announcedAmount;
        private bool _wasDeliverCalled;

        public FluidReplicaBoundaryPort(FluidSimNode pipeNode, double flowCapacityPerTick, double velocity)
        {
            PipeNode = pipeNode;
            FlowCapacityPerTick = flowCapacityPerTick;
            Velocity = velocity;
        }

        public void AnnounceDelivery(double amount)
        {
            _announcedAmount = amount;
        }

        // 複製側が試みた量から予告量を引いた残りを返す。予告が試みた量を超えていれば食い違いだが、ここでは量を切り詰めるだけで判定は呼び出し側が行う
        // Return the attempted amount minus the announced one; an announcement above the attempt is a divergence, but this only clamps and leaves the judgement to the caller
        public FluidStack Deliver(FluidStack fluidStack)
        {
            _wasDeliverCalled = true;
            var remainder = Math.Max(0, fluidStack.Amount - _announcedAmount);
            return new FluidStack(remainder, fluidStack.FluidId);
        }

        // tick末尾に予告を消し、サーバーが渡したのに複製では試みすら無かった不一致を返す
        // Clear the announcement at tick end and report the mismatch where the server delivered but the replica never even attempted
        public bool ClearAnnouncement()
        {
            var unconsumed = 0 < _announcedAmount && !_wasDeliverCalled;
            _announcedAmount = 0;
            _wasDeliverCalled = false;
            return unconsumed;
        }
    }
}
