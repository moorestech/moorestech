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
        // クライアントの基本tick間隔はサーバー更新間隔に合わせる
        // Keep the base client train tick interval aligned with the server update interval.
        private const double TickSeconds = 1d / GameUpdater.TicksPerSecond;
        // この秒差以上で早送り処理を有効化する
        // Enable fast-forward when lag reaches this threshold.
        private const double FastForwardStartLagSeconds = 0.1d;
        private static readonly int FastForwardStartLagTicks = Math.Max(1, (int)Math.Ceiling(FastForwardStartLagSeconds / TickSeconds));

        private readonly TrainUnitClientCache _cache;
        private readonly TrainUnitTickState _tickState;
        private readonly ITrainUnitHashTickGate _hashTickGate;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly List<ClientTrainUnit> _work = new();
        private double _estimatedClientTick;

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
            // 経過時間を積算し、通常進行ぶんのtick予算を算出する
            // Accumulate frame time and compute the normal real-time tick budget.
            _estimatedClientTick += Time.deltaTime / TickSeconds;
            int loopTicks = Mathf.Min((int)((long)Math.Floor(_estimatedClientTick) - _tickState.GetTick()), 2);
            
            for (var i = 0; i < loopTicks; i++)
            {
                _futureMessageBuffer.FlushBySimulatedTick();
                if (!_tickState.IsAllowSimulationNowTick())
                {
                    break;
                }
                _tickState.AdvanceTick();
                
                if (!_hashTickGate.CanAdvanceTick(_tickState.GetTick()))
                {
                    break;
                }
                
                SimulateUpdate();
            }
            
            #region Internal
            void SimulateUpdate()
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
}
