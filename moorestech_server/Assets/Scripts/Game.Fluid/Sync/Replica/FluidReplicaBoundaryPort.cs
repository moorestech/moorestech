using System;
using Core.Master;
using Game.Fluid.Simulation;

namespace Game.Fluid.Sync.Replica
{
    // 複製での境界ポート。機械の受け入れ可否はサーバーが確定した差分で与えるので、予告された量だけを受け取る
    // A boundary port in the replica; the machine's acceptance is dictated by the server's settled diff, so it takes only the announced units
    public sealed class FluidReplicaBoundaryPort : IFluidBoundaryPort
    {
        public int NodeIndex { get; }
        public long FlowCapacityPerTick { get; }
        public long Velocity { get; set; }

        // このtickにサーバーが渡した量(単位)。差分の再生が置き、Deliverが消費する
        // The units the server delivered this tick; set by the diff replay and consumed by Deliver
        private long _announcedUnits;
        private bool _wasDeliverCalled;
        private bool _wasAnnouncementAboveAttempt;

        public FluidReplicaBoundaryPort(int nodeIndex, long flowCapacityPerTick, long velocity)
        {
            NodeIndex = nodeIndex;
            FlowCapacityPerTick = flowCapacityPerTick;
            Velocity = velocity;
        }

        public void AnnounceDelivery(long units)
        {
            _announcedUnits = units;
        }

        // 複製側が試みた量と予告量の小さい方を受け取る。予告が試みた量を超えていれば食い違いとして覚え、tick末尾のClearAnnouncementが報告する
        // Accept the smaller of the attempted and announced units; an announcement above the attempt is remembered as a divergence and reported by ClearAnnouncement at tick end
        public long Deliver(long attemptedUnits, FluidId fluidId)
        {
            _wasDeliverCalled = true;
            if (attemptedUnits < _announcedUnits) _wasAnnouncementAboveAttempt = true;
            return Math.Min(attemptedUnits, _announcedUnits);
        }

        // tick末尾に予告を消し、サーバーが渡したのに複製では試みが無い、または試みが予告より少ない不一致を返す
        // Clear the announcement at tick end and report the mismatch where the server delivered but the replica never attempted or attempted less than announced
        public bool ClearAnnouncement()
        {
            var unconsumed = (0 < _announcedUnits && !_wasDeliverCalled) || _wasAnnouncementAboveAttempt;
            _announcedUnits = 0;
            _wasDeliverCalled = false;
            _wasAnnouncementAboveAttempt = false;
            return unconsumed;
        }
    }
}
