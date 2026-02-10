using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.Network;
using Server.Boot.Loop;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Unit
{
    public sealed class TrainUnitClientSimulator : ITickable
    {
        // クライアントの基本tick間隔はサーバー更新間隔に合わせる
        // Keep the base client train tick interval aligned with the server update interval.
        private const double TickSeconds = ServerGameUpdater.FrameIntervalMs / 1000d;
        // 遅延時のみ1フレームで追加処理するtick上限
        // Maximum extra ticks to process per frame only when lagging behind.
        private const int MaxFastForwardTicksPerFrame = 8;
        // このtick差以上で早送り処理を有効化する
        // Enable fast-forward when lag reaches this threshold.
        private const int FastForwardStartLagTicks = 2;

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
            // 経過時間を積算し、通常進行ぶんのtick予算を算出する
            // Accumulate frame time and compute the normal real-time tick budget.
            _accumulatedSeconds += Time.deltaTime;
            var realtimeBudget = (int)(_accumulatedSeconds / TickSeconds);

            // hash受信tickとの差分が大きい場合だけ追加の早送り予算を付与する
            // Add fast-forward budget only when lag to the received hash tick is large.
            var fastForwardBudget = ComputeFastForwardBudget(realtimeBudget);
            var totalBudget = realtimeBudget + fastForwardBudget;

            for (var i = 0; i < totalBudget; i++)
            {
                if (!_tickState.IsAllowSimulationNowTick())
                {
                    break;
                }
                if (!_hashTickGate.CanAdvanceTick(_tickState.GetTick()))
                {
                    break;
                }
                
                SimulateUpdate();
                
                _futureMessageBuffer.FlushBySimulatedTick();
                
                if (i < realtimeBudget)
                {
                    _accumulatedSeconds -= TickSeconds;
                }
                
                _tickState.AdvanceTick();
            }
            
        #region Internal
            
        int ComputeFastForwardBudget(int realtimeBudget)
        {
            // 受信済みhash tickとの差分から、追加tick数を決定する
            // Determine extra ticks from lag against the latest received hash tick.
            var lagTicks = _tickState.GetHashReceivedTick() - _tickState.GetTick();
            var remainingLagAfterRealtime = lagTicks - realtimeBudget;
            if (remainingLagAfterRealtime < FastForwardStartLagTicks)
            {
                return 0;
            }
            
            return (int)Math.Min(remainingLagAfterRealtime, MaxFastForwardTicksPerFrame);
        }
        
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
