using Core.Update.TickSynchronization;
using System;

namespace Client.Game.Common.TickSynchronization
{
    // クライアントstreamのtick状態を一元管理する。
    // Centralize tick state for a client stream.
    internal sealed class ClientTickState
    {
        private ulong _appliedTickUnifiedId = 0;
        private uint _maxBufferedTicks = 0;

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
            RecordAppliedTickUnifiedId(TickUnifiedIdUtility.CreateTickUnifiedId(tick, tickSequenceId));
        }
        public void RecordAppliedTickUnifiedId(ulong tickUnifiedId)
        {
            if (tickUnifiedId <= _appliedTickUnifiedId)
            {
                return;
            }
            _appliedTickUnifiedId = tickUnifiedId;
        }

        // バッファー済み最大tick
        public void SetMaxBufferedTicks(uint maxBufferedTicks)
        {
            _maxBufferedTicks = Math.Max(_maxBufferedTicks, maxBufferedTicks);
        }
        public uint GetMaxBufferedTicks()
        {
            return _maxBufferedTicks;
        }

        public void AdvanceTick()
        {
            var tick = GetTick() + 1;
            _appliedTickUnifiedId = TickUnifiedIdUtility.CreateTickUnifiedId(tick, 0);
        }
    }
}
