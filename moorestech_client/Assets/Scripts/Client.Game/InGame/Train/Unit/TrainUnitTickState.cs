using System;

namespace Client.Game.InGame.Train.Unit
{
    // クライアント列車シミュレーションのtick状態を一元管理する。
    // Centralize tick state for client train simulation.
    public sealed class TrainUnitTickState
    {
        private long _hashReceivedTick;
        private long _previousHashReceivedTick;
        private bool _hasReceivedHashTick;
        private bool _hasPreviousHashTick;
        private long _tick;

        public long GetTick()
        {
            return _tick;
        }

        public long GetHashReceivedTick()
        {
            return _hashReceivedTick;
        }

        // 直近2回のhash受信tickを返す。
        // Return the latest two received hash ticks.
        public bool TryGetLatestHashTickWindow(out long previousHashTick, out long latestHashTick)
        {
            if (!_hasPreviousHashTick)
            {
                previousHashTick = 0;
                latestHashTick = 0;
                return false;
            }

            previousHashTick = _previousHashReceivedTick;
            latestHashTick = _hashReceivedTick;
            return true;
        }

        // スナップショット適用後の基準tickへ同期する。
        // Align state to the snapshot baseline tick.
        public void SetSnapshotBaselineTick(long serverTick)
        {
            _tick = serverTick;
            if (!_hasReceivedHashTick || serverTick > _hashReceivedTick)
            {
                _hashReceivedTick = serverTick;
                _hasReceivedHashTick = true;
                _hasPreviousHashTick = false;
                return;
            }

            _hashReceivedTick = Math.Max(_hashReceivedTick, serverTick);
        }

        // 受信したhash tickを進行上限として記録する。
        // Record a received hash tick as the simulation upper bound.
        public void RecordHashReceived(long serverTick)
        {
            if (_hasReceivedHashTick && serverTick <= _hashReceivedTick)
            {
                return;
            }

            if (_hasReceivedHashTick)
            {
                _previousHashReceivedTick = _hashReceivedTick;
                _hasPreviousHashTick = true;
            }

            _hashReceivedTick = serverTick;
            _hasReceivedHashTick = true;
        }

        // 現在tickでシミュレーションを進められるか判定する。
        // Check whether simulation can advance at the current tick.
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
