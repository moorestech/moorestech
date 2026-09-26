using Core.Update;
using System;

namespace Client.Game.TickSynchronization
{
    internal sealed class ClientTickAdvanceController
    {
        private const double TickSeconds = 1d / GameUpdater.TicksPerSecond;

        // 閾値を超えた遅延だけを catch-up 対象にする
        // Only the lag that exceeds this threshold is distributed as catch-up
        private const double FastForwardLagSeconds = 0.2d;
        private static readonly double FastForwardLagTicks = Math.Max(1.0, Math.Ceiling(FastForwardLagSeconds / TickSeconds));

        private const int MaxCatchUpTicksPerFrame = 4;

        private readonly ClientTickState _tickState;
        private readonly TickEventBuffer _events;

        private double _estimatedClientTick;
        private double _modifyTick = 0.1;
        private double _modifyTime = 0.1;
        private uint _lastGetMaxBufferedTicks = 0;
        private int _localcnt = 0;

        public ClientTickAdvanceController(ClientTickState state, TickEventBuffer events)
        {
            _tickState = state;
            _events = events;
        }

        public double Advance(float deltaTime, ITickAdvanceGate gate)
        {
            if (_tickState.IsStopped) return _estimatedClientTick;
            _localcnt++;
            _modifyTime *= 0.9991;
            _modifyTick *= 0.9991;
            if ( 20 <= _localcnt && deltaTime < 0.5f)
            {
                _modifyTime += deltaTime;
                _modifyTick += _tickState.GetMaxBufferedTicks() - _lastGetMaxBufferedTicks;
                if (_modifyTime < 1e-5) _modifyTime = 1e-5;
                _estimatedClientTick += deltaTime * (_modifyTick / _modifyTime);
            }
            else
            {
                _modifyTime += TickSeconds;
                _modifyTick += 1.0;
                _estimatedClientTick += deltaTime / TickSeconds;
            }
            // 受信tickへ推定値を補正
            // Correct the estimate against received ticks.
            _lastGetMaxBufferedTicks = _tickState.GetMaxBufferedTicks();
            var pendingTicks = _estimatedClientTick - _lastGetMaxBufferedTicks;
            if (pendingTicks < -FastForwardLagTicks)
            {
                _estimatedClientTick += 1e-4 * pendingTicks * pendingTicks;
            }
            if (0.0 < pendingTicks)
            {
                _estimatedClientTick -= 1e-3 * pendingTicks;
            }

            // gate が許す範囲で、server event を順番に適用して tick を進める
            // Advance ticks while the gate allows it and apply server events in order
            var nextCount = Math.Max(0, _estimatedClientTick - _tickState.GetTick());
            var loopTicks = (int)nextCount;
            if (MaxCatchUpTicksPerFrame <= nextCount)
            {
                _estimatedClientTick = _tickState.GetTick() + MaxCatchUpTicksPerFrame;
                loopTicks = MaxCatchUpTicksPerFrame;
            }

            for (var i = 0; i < loopTicks; i++)
            {
                ulong id = 0;
                while (true)
                {
                    id = _tickState.GetAppliedTickUnifiedId();
                    var isComplete = _events.TryFlushEvent(id + 1);
                    if (!isComplete)
                    {
                        break;
                    }
                }

                // gate停止なら描画 tick を巻き戻しすぎず、次回の同期を待つ
                // If the gate blocks, keep the render tick near the applied tick and wait for the next sync
                var canAdvance = gate.CanAdvanceTick(id + 1);
                if (canAdvance)
                {
                    _tickState.AdvanceTick();
                }
                else
                {
                    _estimatedClientTick = _tickState.GetTick() + 1;
                    break;
                }
            }

            return _estimatedClientTick;
        }
    }
}
