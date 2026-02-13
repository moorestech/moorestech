using System;
using System.Collections.Generic;
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
        private static readonly int FastForwardStartLagTicks = Math.Max(1, (int)Math.Ceiling(FastForwardStartLagSeconds / TickSeconds));

        private readonly TrainUnitClientCache _cache;
        private readonly TrainUnitTickState _tickState;
        private readonly ITrainUnitHashTickGate _hashTickGate;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;

        private double _estimatedClientTick;
        private uint _lastHandledHashTick;
        private bool _hasLastHandledHashTick;
        private double _fastForwardTicksPerSecond;
        private double _fastForwardRemainingTicks;

        public TrainUnitClientSimulator(
            TrainUnitClientCache cache,
            TrainUnitTickState tickState,
            ITrainUnitHashTickGate hashTickGate,
            TrainUnitFutureMessageBuffer futureMessageBuffer)
        {
            _cache = cache;
            _tickState = tickState;
            _hashTickGate = hashTickGate;
            _futureMessageBuffer = futureMessageBuffer;
        }

        public void Tick()
        {
            // 現在フレーム分の通常進行tickを加算する。
            // Add normal tick progress based on the current frame delta.
            _estimatedClientTick += Time.deltaTime / TickSeconds;

            var pendingTicks = (long)Math.Floor(_estimatedClientTick) - _tickState.GetTick();
            int loopTicks = Mathf.Min((int)Math.Max(0, pendingTicks), 2);
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
