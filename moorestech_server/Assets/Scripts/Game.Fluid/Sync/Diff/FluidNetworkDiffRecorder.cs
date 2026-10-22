using System.Collections.Generic;
using Core.Master;
using Game.Fluid.Simulation;

namespace Game.Fluid.Sync.Diff
{
    // サーバーで成立した境界の出入りを、次に取り出されるまで溜める。流体tick直後にポートの実渡し量を写し、ブロック更新中の流入はパイプが書く
    // Accumulates settled boundary exchanges on the server until taken; port deliveries are copied right after the fluid tick, inflows during block updates are written by the pipes
    public sealed class FluidNetworkDiffRecorder
    {
        private readonly List<FluidPortDeliveryRecord> _deliveries = new();
        private readonly List<FluidNodeInflowRecord> _inflows = new();

        // 流体tickの後、Stepperが各ポートの速度へ残した実渡し量を番号順に記録する。0のポートは載せない
        // After the fluid tick, record in port order the actual delivered amount the stepper left in each port's velocity; zero ports are skipped
        public void RecordDeliveries(IReadOnlyList<IFluidBoundaryPort> ports)
        {
            for (var i = 0; i < ports.Count; i++)
            {
                var delivered = ports[i].Velocity;
                if (delivered <= 0) continue;
                _deliveries.Add(new FluidPortDeliveryRecord(i, delivered));
            }
        }

        public void RecordInflow(int nodeIndex, FluidId fluidId, double acceptedAmount)
        {
            _inflows.Add(new FluidNodeInflowRecord(nodeIndex, fluidId, acceptedAmount));
        }

        // 溜まった分を1tickの差分として取り出し、空にする
        // Take the accumulated records as one tick's diff and clear
        public FluidTickDiff TakeTickDiff()
        {
            if (_deliveries.Count == 0 && _inflows.Count == 0) return FluidTickDiff.Empty;
            var diff = new FluidTickDiff(_deliveries.ToArray(), _inflows.ToArray());
            _deliveries.Clear();
            _inflows.Clear();
            return diff;
        }
    }
}
