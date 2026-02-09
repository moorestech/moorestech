using System.Collections.Generic;
using Client.Game.InGame.Train.Network;
using Server.Boot.Loop;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Unit
{
    public sealed class TrainUnitClientSimulator : ITickable
    {
        // サーバー更新周期に合わせてクライアント列車シミュレーションを進める。
        // Align client train simulation step with the server update interval.
        private const double TickSeconds = ServerGameUpdater.FrameIntervalMs / 1000d;
        private readonly TrainUnitClientCache _cache;
        private readonly TrainUnitTickState _tickState;
        private readonly ITrainUnitHashTickGate _hashTickGate;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly List<ClientTrainUnit> _work = new();
        private double _accumulatedSeconds;

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
            // 経過時間を積算し、進行可能な列車tickを順に処理する。
            // Accumulate delta time and process available train ticks.
            _accumulatedSeconds += Time.deltaTime;
            while (_accumulatedSeconds >= TickSeconds)
            {
                if (!_hashTickGate.CanAdvanceTick(_tickState.GetSimulatedTick()))
                {
                    break;
                }

                if (!_tickState.TryGetNextSimulationTick(out var nextTick))
                {
                    break;
                }

                _accumulatedSeconds -= TickSeconds;
                SimulateOneTick();
                _tickState.CompleteSimulationTick(nextTick);
                _futureMessageBuffer.FlushBySimulatedTick();
            }
        }

        #region Internal

        private void SimulateOneTick()
        {
            _cache.CopyUnitsTo(_work);
            for (var i = 0; i < _work.Count; i++)
            {
                _work[i].Update();
            }
        }

        #endregion
    }
}
