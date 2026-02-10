using System;

namespace Client.Game.InGame.Train.Unit
{
    // クライアント列車のシミュレーション/検証tick状態を一元管理する。
    // Centralized tick state for client train simulation and verification.
    public sealed class TrainUnitTickState
    {
        private long _hashVerifiedTick;
        private long _hashReceivedTick;
        private long _tick;

        public long GetHashVerifiedTick()
        {
            return _hashVerifiedTick;
        }

        public long GetTick()
        {
            return _tick;
        }

        public long GetHashReceivedTick()
        {
            return _hashReceivedTick;
        }

        // スナップショット基準tickへ状態を揃える。
        // Align state to the snapshot baseline tick.
        public void SetSnapshotBaselineTick(long serverTick)
        {
            _tick = serverTick;
            _hashVerifiedTick = Math.Max(_hashVerifiedTick, serverTick);
            _hashReceivedTick = Math.Max(_hashReceivedTick, serverTick);
        }

        // 受信済みhash tickに基づき、シミュレーション上限を更新する。
        // Update simulation upper bound from received hash tick.
        public void RecordHashReceived(long serverTick)
        {
            _hashReceivedTick = Math.Max(_hashReceivedTick, serverTick);
        }

        // hash検証通過後に検証済みtickを更新する。
        // Update verified tick after hash validation passes.
        public void RecordHashVerified(long serverTick)
        {
            _hashVerifiedTick = Math.Max(_hashVerifiedTick, serverTick);
        }

        // 1tick先へ進めるかを判定
        // local simulation tick can run.
        public bool IsAllowSimulationNowTick()
        {
            if (_tick >= _hashReceivedTick)
            {
                return false;
            }
            return true;
        }
        public void AdvanceTick()
        {
            _tick++;
        }
    }
}
