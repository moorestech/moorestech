using System;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.View;
using Core.Update;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Unit
{
    public sealed class TrainUnitClientSimulator : ITickable
    {
        // クライアントの基本 tick 間隔は共通のゲーム tick 定義に合わせる
        // Keep the base client tick interval aligned with the shared game tick definition
        private const double TickSeconds = 1d / GameUpdater.TicksPerSecond;

        // 閾値を超えた遅延だけを catch-up 対象にする
        // Only the lag that exceeds this threshold is distributed as catch-up
        private const double FastForwardLagSeconds = 0.2d;
        private static readonly double FastForwardLagTicks = Math.Max(1.0, Math.Ceiling(FastForwardLagSeconds / TickSeconds));

        // 1 フレームで追いつく最大 tick 数を固定する
        // Fix the maximum ticks to catch up in a single frame
        private const int MaxCatchUpTicksPerFrame = 4;

        private readonly TrainUnitTickState _tickState;
        private readonly ITrainUnitHashTickGate _hashTickGate;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitVisualUpdateSystem _visualUpdateSystem;

        private double _estimatedClientTick;
        private double _modifyTick = 0.1;
        private double _modifyTime = 0.1;
        private uint _lastGetMaxBufferedTicks = 0;
        private int _localcnt = 0; 

        public TrainUnitClientSimulator(
            TrainUnitTickState tickState,
            ITrainUnitHashTickGate hashTickGate,
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainUnitVisualUpdateSystem visualUpdateSystem)
        {
            _tickState = tickState;
            _hashTickGate = hashTickGate;
            _futureMessageBuffer = futureMessageBuffer;
            _visualUpdateSystem = visualUpdateSystem;
        }

        public void Tick()
        {
            _localcnt++;
            _modifyTime *= 0.9991;
            _modifyTick *= 0.9991;
            if ( _localcnt >=20 && Time.deltaTime < 0.5f)
            {
                _modifyTime += Time.deltaTime;
                _modifyTick += _tickState.GetMaxBufferedTicks() - _lastGetMaxBufferedTicks;
                if (_modifyTime < 1e-5) _modifyTime = 1e-5;
                _estimatedClientTick += Time.deltaTime * (_modifyTick / _modifyTime);
                if (_localcnt % 64 == 10) Debug.Log(_modifyTick / _modifyTime);
            }
            else
            {
                _modifyTime += TickSeconds;
                _modifyTick += 1.0;
                _estimatedClientTick += Time.deltaTime / TickSeconds;
            }
            // 現在フレーム分の通常進行 tick を加算する
            // Add normal tick progress based on the current frame delta
            //_estimatedClientTick += Time.deltaTime / TickSeconds;
            _lastGetMaxBufferedTicks = _tickState.GetMaxBufferedTicks();
            var pendingTicks = _estimatedClientTick - _lastGetMaxBufferedTicks;
            if (pendingTicks < -FastForwardLagTicks)
            {
                _estimatedClientTick += 1e-4 * pendingTicks * pendingTicks;
            }
            if (pendingTicks > 0.0)
            {
                _estimatedClientTick -= 1e-3 * pendingTicks;
            }

            // hash gate が許す範囲で、server event を順番に適用して tick を進める
            // Advance ticks while the hash gate allows it and apply server events in order
            var nextCount = Math.Max(0, _estimatedClientTick - _tickState.GetTick());
            var loopTicks = (int)nextCount;
            if (nextCount >= MaxCatchUpTicksPerFrame)
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
                    var isComplete = _futureMessageBuffer.TryFlushEvent(id + 1);
                    if (!isComplete)
                    {
                        break;
                    }
                }

                // hash 不一致なら描画 tick を巻き戻しすぎず、次回の同期を待つ
                // If the hash mismatches, keep the render tick near the applied tick and wait for the next sync
                var canAdvance = _hashTickGate.CanAdvanceTick(id + 1);
                if (canAdvance)
                {
                    _tickState.AdvanceTick();
                }
                else
                {
                    _estimatedClientTick = _tickState.GetTick() + 1;
                    Debug.Log("TickGate");
                    break;
                }
            }

            // simulator が推定した render tick を、TrainUnit 全体の描画更新 system に渡す
            // Pass the simulator-estimated render tick to the visual update system for all TrainUnits
            _visualUpdateSystem.UpdateAll(_estimatedClientTick, _tickState.GetTick());
        }
    }
}
