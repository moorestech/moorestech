namespace Client.Game.InGame.Train.Unit
{
    // tick と tickSequenceId を単一の比較キーに統合する。
    // Compose tick and tickSequenceId into a single monotonic order key.
    public static class TrainTickUnifiedIdUtility
    {
        // 上位32bitにtick、下位32bitにtickSequenceIdを詰める。
        // Pack tick into high 32 bits and tickSequenceId into low 32 bits.
        public static ulong CreateTickUnifiedId(uint tick, uint tickSequenceId)
        {
            return ((ulong)tick << 32) | tickSequenceId;
        }

        // 統合IDから上位32bitのtickを取り出す。
        // Extract high 32-bit tick from unified id.
        public static uint ExtractTick(ulong tickUnifiedId)
        {
            return (uint)(tickUnifiedId >> 32);
        }

        // 統合IDから下位32bitのtickSequenceIdを取り出す。
        // Extract low 32-bit tickSequenceId from unified id.
        public static uint ExtractTickSequenceId(ulong tickUnifiedId)
        {
            return (uint)(tickUnifiedId & uint.MaxValue);
        }
    }

    // クライアント列車シミュレーションのtick状態を一元管理する。
    // Centralize tick state for client train simulation.
    public sealed class TrainUnitTickState
    {
        private uint _hashReceivedTick;
        private uint _previousHashReceivedTick;
        private bool _hasReceivedHashTick;
        private bool _hasPreviousHashTick;
        private uint _tick;
        private uint _lastHashVerifiedTick;
        private ulong _appliedTickUnifiedId;

        public uint GetTick()
        {
            return _tick;
        }

        public uint GetHashReceivedTick()
        {
            return _hashReceivedTick;
        }

        public uint GetLastHashVerifiedTick()
        {
            return _lastHashVerifiedTick;
        }

        public uint GetAppliedTickSequenceId()
        {
            return TrainTickUnifiedIdUtility.ExtractTickSequenceId(_appliedTickUnifiedId);
        }

        public ulong GetAppliedTickUnifiedId()
        {
            return _appliedTickUnifiedId;
        }

        // 適用済みの最大tickUnifiedIdを更新する。
        // Update the highest applied tickUnifiedId.
        public void RecordAppliedTickUnifiedId(uint tick, uint tickSequenceId)
        {
            var tickUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(tick, tickSequenceId);
            if (tickUnifiedId <= _appliedTickUnifiedId)
            {
                return;
            }
            _appliedTickUnifiedId = tickUnifiedId;
        }

        // 直近2回のhash受信tickを返す。
        // Return the latest two received hash ticks.
        public bool TryGetLatestHashTickWindow(out uint previousHashTick, out uint latestHashTick)
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
        public void SetSnapshotBaseline(uint serverTick, uint tickSequenceId)
        {
            _tick = serverTick;
            _lastHashVerifiedTick = serverTick;
            _appliedTickUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(serverTick, tickSequenceId);
            if (!_hasReceivedHashTick || serverTick > _hashReceivedTick)
            {
                _hashReceivedTick = serverTick;
                _hasReceivedHashTick = true;
                _hasPreviousHashTick = false;
                return;
            }

            if (serverTick > _hashReceivedTick)
            {
                _hashReceivedTick = serverTick;
            }
        }

        // hash検証が完了したtickを記録する。
        // Record the latest tick that passed hash validation.
        public void RecordHashVerified(uint serverTick)
        {
            if (serverTick <= _lastHashVerifiedTick)
            {
                return;
            }
            _lastHashVerifiedTick = serverTick;
        }

        // 受信したhash tickを進行上限として記録する。
        // Record a received hash tick as the simulation upper bound.
        public void RecordHashReceived(uint serverTick)
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
            if (_tick > _lastHashVerifiedTick)
            {
                return false;
            }
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
