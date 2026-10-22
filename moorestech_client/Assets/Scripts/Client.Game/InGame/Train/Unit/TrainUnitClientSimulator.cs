using System;
using Client.Game.InGame.BeltTransport.Cover;
using Client.Game.InGame.BeltTransport.View;
using Client.Game.InGame.FluidNetwork.View;
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

        // 余裕 = 受信済み最先端+1 − 推定tick（受信待ちで止まるまでのtick数）。受信は1フレーム単位で届くので、フレーム時間ぶんを下限にする
        // Slack = newest received tick + 1 − estimate (ticks left before a data stall); arrivals land per frame, so one frame's worth is the floor
        private const double MinTargetSlackTicks = 0.35;
        private const double MaxTargetSlackTicks = 2.0;
        private const double TargetSlackFrameFactor = 1.3;
        // 受信待ちで止まるたびに目標の余裕を広げ、止まらない間はゆっくり縮めて詰めていく
        // Widen the target slack every time we stall waiting for data, and shrink it slowly while no stall happens
        private const double TargetSlackBumpTicks = 0.3;
        private const double TargetSlackDecayTicksPerSecond = 0.02;

        // 余裕は到着ごとに1tick分の鋸歯を描くので、平滑化した値を目標(鋸歯の中央=目標+0.5)へ寄せる。見た目の速度変化が分からない範囲に収める
        // Slack is a one-tick sawtooth per arrival, so the smoothed value is steered to the target (sawtooth middle = target + 0.5) within a range the eye does not notice
        private const double SlackSmoothingSeconds = 0.5;
        private const double SpeedGainPerTick = 0.5;
        private const double MinSpeedFactor = 0.9;
        private const double MaxSpeedFactor = 1.1;

        // 閾値を超えた遅延だけを catch-up 対象にする
        // Only the lag that exceeds this threshold is distributed as catch-up
        private const double FastForwardLagSeconds = 0.2d;
        private static readonly double FastForwardLagTicks = Math.Max(1.0, Math.Ceiling(FastForwardLagSeconds / TickSeconds));

        // 1 フレームで追いつく最大 tick 数。低fps(Sceneタブ併用で1〜3fps等)でも1秒ぶん以上を1フレームで回せる値にしておく(暫定の決め打ち・2026-10-14)
        // Maximum ticks to catch up in a single frame; large enough to cover over a second per frame at low fps (1-3fps with the Scene tab open), a provisional hard-coded value (2026-10-14)
        private const int MaxCatchUpTicksPerFrame = 21;

        private readonly TrainUnitTickState _tickState;
        private readonly ITrainUnitHashTickGate _hashTickGate;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitVisualUpdateSystem _visualUpdateSystem;
        private readonly BeltItemObjectPresenter _beltItemObjectPresenter;
        private readonly BeltMergeCellCoverPresenter _beltMergeCellCoverPresenter;
        private readonly FluidPipeWaterLevelPresenter _fluidPipeWaterLevelPresenter;

        private double _estimatedClientTick;
        private double _targetSlackTicks = MinTargetSlackTicks;
        private double _smoothedSlackTicks = MinTargetSlackTicks + 0.5;
        private bool _isRecoveringFromStarvation;
        private double _modifyTick = 0.1;
        private double _modifyTime = 0.1;
        private uint _lastGetMaxBufferedTicks = 0;
        private int _localcnt = 0;

        public TrainUnitClientSimulator(
            TrainUnitTickState tickState,
            ITrainUnitHashTickGate hashTickGate,
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainUnitVisualUpdateSystem visualUpdateSystem,
            BeltItemObjectPresenter beltItemObjectPresenter,
            BeltMergeCellCoverPresenter beltMergeCellCoverPresenter,
            FluidPipeWaterLevelPresenter fluidPipeWaterLevelPresenter)
        {
            _tickState = tickState;
            _hashTickGate = hashTickGate;
            _futureMessageBuffer = futureMessageBuffer;
            _visualUpdateSystem = visualUpdateSystem;
            _beltItemObjectPresenter = beltItemObjectPresenter;
            _beltMergeCellCoverPresenter = beltMergeCellCoverPresenter;
            _fluidPipeWaterLevelPresenter = fluidPipeWaterLevelPresenter;
        }

        public void Tick()
        {
            var deltaTime = Time.deltaTime;
            var arrivalRate = UpdateArrivalRate(deltaTime);

            // 目標の余裕は止まるたび広がり、止まらなければフレーム時間ぶんまで縮む
            // The target slack widens on each stall and shrinks back to one frame's worth otherwise
            var frameSlackFloor = Math.Max(MinTargetSlackTicks, TargetSlackFrameFactor * Time.smoothDeltaTime / TickSeconds);
            _targetSlackTicks = Math.Max(frameSlackFloor, _targetSlackTicks - TargetSlackDecayTicksPerSecond * deltaTime);
            _lastGetMaxBufferedTicks = _tickState.GetMaxBufferedTicks();

            // 初期同期やスナップショット適用で適用済みtickが推定を1tick超えて飛び越えたら、推定もそこへ合わせる（停止明けの引き下げは1tick以内なので対象外）
            // When the initial sync or a snapshot apply moves the applied tick more than one tick past the estimate, move the estimate up to it (the post-stall pullback stays within one tick, so it is left alone)
            if (_estimatedClientTick < _tickState.GetTick() - 1.0) _estimatedClientTick = _tickState.GetTick();
            // 平滑化した余裕を目標へ寄せる速さで推定tickを進める
            // Advance the estimate at a speed that steers the smoothed slack toward the target
            var slackTicks = _lastGetMaxBufferedTicks + 1.0 - _estimatedClientTick;
            var smoothing = Math.Min(1.0, deltaTime / SlackSmoothingSeconds);
            _smoothedSlackTicks += (slackTicks - _smoothedSlackTicks) * smoothing;
            _estimatedClientTick += deltaTime * arrivalRate * ResolveSpeedFactor(_smoothedSlackTicks - (_targetSlackTicks + 0.5));

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

                // hash gateが進行を許可しない場合は推定tickを現在tick+1へ補正し、このフレームの列車tickループを抜ける
                // If the hash gate disallows advancement, set the estimated tick to the current tick + 1 and leave this frame's train tick loop
                var canAdvance = _hashTickGate.CanAdvanceTick(id + 1);
                if (canAdvance)
                {
                    _tickState.AdvanceTick();
                    if (!_isRecoveringFromStarvation) continue;

                    // 受信待ちの停止が明けた直後、推定が目標の余裕を割っていれば最先端まで下げて余裕を作る。描画は止まっていた位置に1フレーム未満留まるだけで戻らない
                    // Right after a data stall ends, if the estimate is short of the target slack pull it back no further than the newest tick; the render stays where it froze for under a frame and never rewinds
                    _isRecoveringFromStarvation = false;
                    var pulledBack = Math.Max(_lastGetMaxBufferedTicks, _lastGetMaxBufferedTicks + 1.0 - _targetSlackTicks);
                    _estimatedClientTick = Math.Min(_estimatedClientTick, pulledBack);
                    _smoothedSlackTicks = _lastGetMaxBufferedTicks + 1.0 - _estimatedClientTick;
                    break;
                }
                _estimatedClientTick = _tickState.GetTick() + 1;

                // 受信済みを使い切って止まったなら、次からは今より手前を目標にして同じ止まり方を避ける
                // If we stalled because the received ticks ran out, aim further back from now on so the same stall does not repeat
                if (_tickState.GetTick() == _lastGetMaxBufferedTicks && !_isRecoveringFromStarvation)
                {
                    _isRecoveringFromStarvation = true;
                    _targetSlackTicks = Math.Min(MaxTargetSlackTicks, _targetSlackTicks + TargetSlackBumpTicks);
                }
                break;
            }

            // simulator が推定した render tick を、TrainUnit 全体の描画更新 system に渡す
            // Pass the simulator-estimated render tick to the visual update system for all TrainUnits
            _visualUpdateSystem.UpdateAll(_estimatedClientTick, _tickState.GetTick());

            // ベルト上のアイテムの表示は列車の描画更新の後に同じ順で更新する(複製が進んだ回だけ作り直す)
            // Belt item display updates after the train visuals in this same fixed order (rebuilt only when the replica advanced)
            _beltItemObjectPresenter.UpdateAll();

            // 合流マスの覆いはアイテム表示の後に同じ順で更新する(複製が進んだ回に合流マスを取り直す)
            // Merge-cell covers update after the item display in this same fixed order (merge cells re-read when the replica advanced)
            _beltMergeCellCoverPresenter.UpdateAll();

            // パイプの水位は流体の複製が進んだ回だけ、同じ順で押し込む
            // Pipe water levels are pushed in this same fixed order, only when the fluid replica advanced
            _fluidPipeWaterLevelPresenter.UpdateAll();
        }

        // 受信tickの到着ペース(tick/秒)を長い時定数で平滑化する。起動直後と長いフレームは名目値で数える
        // Smooth the arrival pace of received ticks (ticks per second) with a long time constant; right after start and on long frames count the nominal pace
        private double UpdateArrivalRate(float deltaTime)
        {
            _localcnt++;
            _modifyTime *= 0.9991;
            _modifyTick *= 0.9991;
            if (_localcnt >= 20 && deltaTime < 0.5f)
            {
                _modifyTime += deltaTime;
                _modifyTick += _tickState.GetMaxBufferedTicks() - _lastGetMaxBufferedTicks;
                if (_modifyTime < 1e-5) _modifyTime = 1e-5;
                return _modifyTick / _modifyTime;
            }
            _modifyTime += TickSeconds;
            _modifyTick += 1.0;
            return 1.0 / TickSeconds;
        }

        // 余裕が目標より多ければ速く、少なければ遅く進める。大きく遅れたときだけ見た目を捨てて追いつく
        // Run faster when slack exceeds the target and slower when it falls short; only a large lag sacrifices the look to catch up
        private static double ResolveSpeedFactor(double slackErrorTicks)
        {
            if (FastForwardLagTicks < slackErrorTicks)
            {
                return 1.0 + slackErrorTicks * SpeedGainPerTick;
            }
            return Math.Clamp(1.0 + slackErrorTicks * SpeedGainPerTick, MinSpeedFactor, MaxSpeedFactor);
        }
    }
}
