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

        public void AdvanceTick()
        {
            var tick = GetTick() + 1;
            _appliedTickUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(tick, 0);
        }
    }
}
