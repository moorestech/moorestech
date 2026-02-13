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
        private readonly List<ClientTrainUnit> _work = new();

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
            RefreshFastForwardScheduleByHashWindow();

            // 現在フレーム分の通常進行tickを加算する。
            // Add normal tick progress based on the current frame delta.
            _estimatedClientTick += Time.deltaTime / TickSeconds;
            ApplyScheduledFastForward();

            var pendingTicks = (long)Math.Floor(_estimatedClientTick) - _tickState.GetTick();
            int loopTicks = Mathf.Min((int)Math.Max(0, pendingTicks), 2);
            for (var i = 0; i < loopTicks; i++)
            {
                if (!_tickState.IsAllowSimulationNowTick())
                {
                    // hash未検証tickに滞留している場合は、現在tickのhash照合だけ試行する。
                    // When blocked on an unverified tick, try hash validation for the current tick only.
                    FlushAndSimulateCurrentTickIfRequested();
                    _hashTickGate.CanAdvanceTick(_tickState.GetTick());
                    _estimatedClientTick = _tickState.GetTick() + 1;
                    break;
                }
                
                _tickState.AdvanceTick();
                FlushAndSimulateCurrentTickIfRequested();
                
                if (!_hashTickGate.CanAdvanceTick(_tickState.GetTick()))
                {
                    break;
                }
            }

            #region Internal
            /// 人間向け解説(コーディングAIはロジック変更がない限りこのコメントをけさないように)
            /// TrainUnitTickStateでRecordHashReceivedで受信した時点で、まだTrainUnitFutureMessageBufferに前回のhashが残っている状況があると思います。
            /// その場合では、TrainUnitClientSimulatorの_estimatedClientTickが前回hashのtickに追いついていない状況です。
            /// この場合早送りすることが推奨されます。private const double FastForwardStartLagSeconds = 0.1d;はマージン秒です。
            /// 現在の_estimatedClientTickが前回hashのtickに0.1秒以上遅れて追いついていない場合に、次のhashが届くまで均等に早送りをする、という実装をしたのが
            /// RefreshFastForwardScheduleByHashWindow()や
            /// _fastForwardTicksPerSecond
            /// _fastForwardRemainingTicks　など
            /// つまり_estimatedClientTickが123.4で前回hashのtickが126、現在ちょうど130tick目のhashが届いた状況では126から FastForwardStartLagSeconds = 0.1分へらして124tickには届いてほしい
            /// 0.6tick分早まわししたいわけです。この0.6を134tick目のhashが届くであろうタイミングまで均等にして当クラスのTick()で追従します
            void RefreshFastForwardScheduleByHashWindow()
            {
                var latestHashTick = _tickState.GetHashReceivedTick();
                if (_hasLastHandledHashTick && latestHashTick <= _lastHandledHashTick)
                {
                    return;
                }

                _lastHandledHashTick = latestHashTick;
                _hasLastHandledHashTick = true;
                if (!_tickState.TryGetLatestHashTickWindow(out var previousHashTick, out var currentHashTick))
                {
                    ResetFastForwardSchedule();
                    return;
                }

                // 前回hash tickに対して遅延しきい値を超えたぶんを算出する。
                // Compute lag excess against the previous hash tick.
                var lagToPreviousHashTicks = previousHashTick - _estimatedClientTick;
                var requiredFastForwardTicks = lagToPreviousHashTicks - FastForwardStartLagTicks;
                if (requiredFastForwardTicks <= 0d)
                {
                    ResetFastForwardSchedule();
                    return;
                }

                // 直近hash間隔の次区間に向けて、早送り量を均等配分する。
                // Evenly distribute the extra ticks over the next expected hash interval.
                var hashIntervalTicks = Math.Max(1d, currentHashTick - previousHashTick);
                var distributeDurationSeconds = hashIntervalTicks * TickSeconds;
                _fastForwardTicksPerSecond = requiredFastForwardTicks / distributeDurationSeconds;
                _fastForwardRemainingTicks = requiredFastForwardTicks;
                
                void ResetFastForwardSchedule()
                {
                    _fastForwardTicksPerSecond = 0d;
                    _fastForwardRemainingTicks = 0d;
                }
            }

            void ApplyScheduledFastForward()
            {
                if (_fastForwardTicksPerSecond <= 0d || _fastForwardRemainingTicks <= 0d)
                {
                    return;
                }

                var additionalTicks = Time.deltaTime * _fastForwardTicksPerSecond;
                if (additionalTicks <= 0d)
                {
                    return;
                }

                var consumedTicks = Math.Min(additionalTicks, _fastForwardRemainingTicks);
                _estimatedClientTick += consumedTicks;
                _fastForwardRemainingTicks -= consumedTicks;
            }

            void SimulateUpdate()
            {
                _cache.CopyUnitsTo(_work);
                for (var i = 0; i < _work.Count; i++)
                {
                    _work[i].Update();
                }
            }

            void FlushAndSimulateCurrentTickIfRequested()
            {
                // preイベントを順序どおり適用し、要求された回数だけsimを実行する。
                // Apply pre events in order, then run simulation at most once if requested.
                _futureMessageBuffer.FlushPreBySimulatedTick();
                if (_futureMessageBuffer.TryConsumeSimulationRequest())
                {
                    SimulateUpdate();
                }
                _futureMessageBuffer.FlushPostBySimulatedTick();
            }

            #endregion
        }
    }
}
