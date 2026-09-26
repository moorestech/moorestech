using Core.Update.TickSynchronization;
using System;

namespace Client.Game.InGame.Train.Unit
{
    internal sealed class TrainUnitTickState
    {
        internal bool IsStopped { get; private set; }

        // 停止理由を一度記録し、以後のstream適用を止める
        // Record the stop reason once and prevent subsequent stream application
        internal void Stop(string reason)
        {
            if (IsStopped) return;
            IsStopped = true;
            UnityEngine.Debug.LogError(reason);
        }

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
            RecordAppliedTickUnifiedId(TrainTickUnifiedIdUtility.CreateTickUnifiedId(tick, tickSequenceId));
        }
        public void RecordAppliedTickUnifiedId(ulong tickUnifiedId)
        {
            if (IsStopped || tickUnifiedId <= _appliedTickUnifiedId)
            {
                return;
            }
            _appliedTickUnifiedId = tickUnifiedId;
        }

        // eventとhashに同じ受信境界を適用する。
        // Apply the same receive boundary to events and hashes.
        internal bool TryAcceptReceivedTickUnifiedId(ulong tickUnifiedId)
        {
            if (IsStopped || tickUnifiedId <= _appliedTickUnifiedId)
            {
                return false;
            }
            SetMaxBufferedTicks((uint)(tickUnifiedId >> 32));
            return true;
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
            _appliedTickUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(tick, 0);
        }
    }
}
