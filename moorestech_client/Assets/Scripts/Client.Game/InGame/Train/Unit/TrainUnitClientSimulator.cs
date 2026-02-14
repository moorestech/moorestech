using System;
using Client.Game.InGame.Train.Network;
using Core.Update;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Unit
{
    public sealed class TrainUnitClientSimulator : ITickable
    {
        // クライアントの基本tick間隔は共通のゲームtick定義に合わせる。
        // Keep the base client tick interval aligned with the shared game tick definition.
        private const double TickSeconds = 1d / GameUpdater.TicksPerSecond;

        // この遅延秒数を超えたぶんだけを均等早送り対象にする。
        // Only the lag that exceeds this threshold is distributed as fast-forward.
        private const double FastForwardStartLagSeconds = 0.1d;
        private static readonly double FastForwardStartLagTicks = Math.Max(1.0, Math.Ceiling(FastForwardStartLagSeconds / TickSeconds));
        // 1フレームで追いつく最大Tick数
        // Maximum ticks to catch up in a single frame.
        private const int MaxCatchUpTicksPerFrame = 4;

        private readonly TrainUnitTickState _tickState;
        private readonly ITrainUnitHashTickGate _hashTickGate;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;

        private double _estimatedClientTick;
        private uint _lastHandledHashTick;
        private bool _hasLastHandledHashTick;
        private double _fastForwardTicksPerSecond;
        private double _fastForwardRemainingTicks;

        public TrainUnitClientSimulator(
            TrainUnitTickState tickState,
            ITrainUnitHashTickGate hashTickGate,
            TrainUnitFutureMessageBuffer futureMessageBuffer)
        {
            _tickState = tickState;
            _hashTickGate = hashTickGate;
            _futureMessageBuffer = futureMessageBuffer;
        }

        public void Tick()
        {
            // 現在フレーム分の通常進行tickを加算する。
            // Add normal tick progress based on the current frame delta.
            _estimatedClientTick += Time.deltaTime / TickSeconds;
            double pendingTicks = Math.Floor(_estimatedClientTick) - _tickState.GetMaxBufferedTicks();
            if (pendingTicks < -FastForwardStartLagTicks)
            {
                _estimatedClientTick += 0.5 * (_tickState.GetMaxBufferedTicks() - _estimatedClientTick) + 0.1;
            }
            int loopTicks = Mathf.Min((int)Math.Max(0, Math.Floor(_estimatedClientTick) - _tickState.GetTick()), MaxCatchUpTicksPerFrame);
            
            for (var i = 0; i < loopTicks; i++)
            {
                ulong id = 0;
                // キューされたeventを連番で処理
                while (true)
                {
                    id = _tickState.GetAppliedTickUnifiedId();
                    var iscomplete = _futureMessageBuffer.TryFlushEvent(id + 1);
                    if (!iscomplete) break;
                }
                
                var canAdvance = _hashTickGate.CanAdvanceTick(id + 1);
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
        }
    }
}
