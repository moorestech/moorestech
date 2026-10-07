using System;

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
    }

    // クライアント列車シミュレーションのtick状態を一元管理する。
    // Centralize tick state for client train simulation.
    public sealed class TrainUnitTickState
    {
        private ulong _appliedTickUnifiedId = 0;
        private ulong _maxBufferedTickUnifiedId = 0;
        private SynchronizationPhase _phase;
        internal bool IsInitialized => _phase != SynchronizationPhase.AwaitingInitialSnapshot;
        internal bool IsPermanentlyWaiting => _phase == SynchronizationPhase.PermanentlyWaiting;

        internal void Initialize(ulong appliedId)
        {
            // 初期snapshotの基準を確定してから、順序付き進行を許可する。
            // Establish the initial snapshot baseline before allowing ordered progress.
            RecordAppliedTickUnifiedId(appliedId);
            SetMaxBufferedTickUnifiedId(appliedId);
            _phase = SynchronizationPhase.Running;
        }

        internal void StopPermanently()
        {
            _phase = SynchronizationPhase.PermanentlyWaiting;
        }
        
        // 統合IDから上位32bitのtickを取り出す。
        // Extract high 32-bit tick from unified id.
        public uint GetTick()
        {
            return (uint)(_appliedTickUnifiedId >> 32);
        }
        // 下位32bitをとりだす
        // Extract low 32-bit tickSequenceId from unified id.
        public uint GetTickSequenceId()
        {
            return (uint)(_appliedTickUnifiedId & 0xFFFFFFFF);
        }

        public ulong GetAppliedTickUnifiedId()
        {
            return _appliedTickUnifiedId;
        }

        // 適用済みの最大tickUnifiedIdを更新する。
        // Update the highest applied tickUnifiedId.
        public void RecordAppliedTickUnifiedId(uint tick, uint tickSequenceId)
        {
            RecordAppliedTickUnifiedId(TrainTickUnifiedIdUtility.CreateTickUnifiedId(tick, tickSequenceId));
        }
        public void RecordAppliedTickUnifiedId(ulong tickUnifiedId)
        {
            if (tickUnifiedId <= _appliedTickUnifiedId)
            {
                return;
            }
            _appliedTickUnifiedId = tickUnifiedId;
        }
        
        // 受信済みの最大統合IDを保持する。
        // Retain the highest received unified ID.
        public void SetMaxBufferedTickUnifiedId(ulong tickUnifiedId)
        {
            _maxBufferedTickUnifiedId = Math.Max(_maxBufferedTickUnifiedId, tickUnifiedId);
        }
        internal ulong GetMaxBufferedTickUnifiedId()
        {
            return _maxBufferedTickUnifiedId;
        }
        public uint GetMaxBufferedTicks()
        {
            return (uint)(_maxBufferedTickUnifiedId >> 32);
        }

        public void AdvanceTick()
        {
            var tick = GetTick() + 1;
            _appliedTickUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(tick, 0);
        }

        private enum SynchronizationPhase
        {
            AwaitingInitialSnapshot,
            Running,
            PermanentlyWaiting,
        }
    }
}
