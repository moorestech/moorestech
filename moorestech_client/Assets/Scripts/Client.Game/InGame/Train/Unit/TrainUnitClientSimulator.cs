using System;
using System.Globalization;
using System.IO;
using System.Text;
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
        private const string TickVisualizationFolderName = "moor2";
        private const string TickVisualizationToolFolderName = "train_tick_visualizer";
        private const int TickVisualizationFlushRows = 8;

        private readonly TrainUnitTickState _tickState;
        private readonly ITrainUnitHashTickGate _hashTickGate;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitRenderInterpolationState _renderInterpolationState;

        private double _estimatedClientTick;
        private string _tickVisualizationLogPath;
        private bool _tickVisualizationLogInitialized;
        private readonly StringBuilder _tickVisualizationLogBuffer = new();
        private int _tickVisualizationBufferedRows;

        public TrainUnitClientSimulator(
            TrainUnitTickState tickState,
            ITrainUnitHashTickGate hashTickGate,
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainUnitRenderInterpolationState renderInterpolationState)
        {
            _tickState = tickState;
            _hashTickGate = hashTickGate;
            _futureMessageBuffer = futureMessageBuffer;
            _renderInterpolationState = renderInterpolationState;
        }

        public void Tick()
        {
            var logTime = Time.timeAsDouble;
            var logFrame = Time.frameCount;
            var estimatedTickBefore = _estimatedClientTick;
            var appliedTickBefore = _tickState.GetTick();
            var maxBufferedTickBefore = _tickState.GetMaxBufferedTicks();

            // 現在フレーム分の通常進行tickを加算する。
            // Add normal tick progress based on the current frame delta.
            _estimatedClientTick += Time.deltaTime / TickSeconds;
            double pendingTicks = Math.Floor(_estimatedClientTick) - _tickState.GetMaxBufferedTicks();
            var fastForwardApplied = false;
            if (pendingTicks < -FastForwardStartLagTicks)
            {
                fastForwardApplied = true;
                _estimatedClientTick += 0.5 * (_tickState.GetMaxBufferedTicks() - _estimatedClientTick) + 0.1;
            }
            int loopTicks = Mathf.Min((int)Math.Max(0, Math.Floor(_estimatedClientTick) - _tickState.GetTick()), MaxCatchUpTicksPerFrame);
            var advancedTicks = 0;
            var flushedEvents = 0;
            var blockedByHashGate = false;
            
            for (var i = 0; i < loopTicks; i++)
            {
                ulong id = 0;
                // キューされたeventを連番で処理
                while (true)
                {
                    id = _tickState.GetAppliedTickUnifiedId();
                    var iscomplete = _futureMessageBuffer.TryFlushEvent(id + 1);
                    if (!iscomplete) break;
                    flushedEvents++;
                }
                
                var canAdvance = _hashTickGate.CanAdvanceTick(id + 1);
                if (canAdvance)
                {
                    _tickState.AdvanceTick();
                    advancedTicks++;
                }
                else
                {
                    blockedByHashGate = true;
                    _estimatedClientTick = _tickState.GetTick() + 1;
                    break;             
                }
            }

            // 描画補間率を表示側が simulator に依存せず読めるように記録する
            // Record render interpolation rate so view code can avoid depending on the simulator
            RecordRenderInterpolationRate();
            RecordTickVisualizationLog();

            #region Internal

            void RecordRenderInterpolationRate()
            {
                var progress = _estimatedClientTick - _tickState.GetTick();
                _renderInterpolationState.SetRenderInterpolationRate(progress);
            }

            void RecordTickVisualizationLog()
            {
                // tickバッファの余裕と停止・追従要因をCSVへ出す。
                // Write tick buffer headroom and stop/catch-up signals to CSV.
                EnsureTickVisualizationLogInitialized();

                var appliedTickAfter = _tickState.GetTick();
                var maxBufferedTickAfter = _tickState.GetMaxBufferedTicks();
                var bufferLeadAfter = maxBufferedTickAfter - _estimatedClientTick;
                var renderInterpolationRate = _renderInterpolationState.GetRenderInterpolationRate();
                var row = string.Join(",",
                    Format(logTime),
                    logFrame.ToString(CultureInfo.InvariantCulture),
                    Format(Time.deltaTime),
                    Format(estimatedTickBefore),
                    Format(_estimatedClientTick),
                    appliedTickBefore.ToString(CultureInfo.InvariantCulture),
                    appliedTickAfter.ToString(CultureInfo.InvariantCulture),
                    maxBufferedTickBefore.ToString(CultureInfo.InvariantCulture),
                    maxBufferedTickAfter.ToString(CultureInfo.InvariantCulture),
                    Format(bufferLeadAfter),
                    Format(pendingTicks),
                    Format(FastForwardStartLagSeconds),
                    Format(FastForwardStartLagTicks),
                    fastForwardApplied ? "1" : "0",
                    loopTicks.ToString(CultureInfo.InvariantCulture),
                    advancedTicks.ToString(CultureInfo.InvariantCulture),
                    flushedEvents.ToString(CultureInfo.InvariantCulture),
                    blockedByHashGate ? "1" : "0",
                    Format(renderInterpolationRate));
                _tickVisualizationLogBuffer.AppendLine(row);
                _tickVisualizationBufferedRows++;
                if (_tickVisualizationBufferedRows >= TickVisualizationFlushRows || blockedByHashGate || fastForwardApplied)
                {
                    FlushTickVisualizationLogBuffer();
                }
            }

            void EnsureTickVisualizationLogInitialized()
            {
                if (_tickVisualizationLogInitialized)
                {
                    return;
                }

                // Desktop配下の解析フォルダへ、プレイごとに別CSVを作る。
                // Create one CSV per play session under the Desktop analysis folder.
                var desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
                var logDirectory = Path.Combine(desktop, TickVisualizationFolderName, TickVisualizationToolFolderName, "logs");
                Directory.CreateDirectory(logDirectory);
                _tickVisualizationLogPath = Path.Combine(logDirectory, $"train_tick_client_simulator_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                File.WriteAllText(
                    _tickVisualizationLogPath,
                    "unityTime,frame,deltaTime,estimatedClientTickBefore,estimatedClientTickAfter,appliedTickBefore,appliedTickAfter,maxBufferedTickBefore,maxBufferedTickAfter,bufferLeadAfter,pendingTicksBefore,fastForwardStartLagSeconds,fastForwardStartLagTicks,fastForwardApplied,loopTicksPlanned,advancedTicks,flushedEvents,blockedByHashGate,renderInterpolationRate" + System.Environment.NewLine);
                _tickVisualizationLogInitialized = true;
                Debug.Log($"[TrainTickVisualization] writing CSV: {_tickVisualizationLogPath}");
            }

            void FlushTickVisualizationLogBuffer()
            {
                if (_tickVisualizationBufferedRows == 0)
                {
                    return;
                }

                // 計測負荷を抑えるため、短いバッファ単位でまとめて追記する。
                // Append in small batches to keep the measurement overhead low.
                File.AppendAllText(_tickVisualizationLogPath, _tickVisualizationLogBuffer.ToString());
                _tickVisualizationLogBuffer.Clear();
                _tickVisualizationBufferedRows = 0;
            }

            string Format(double value)
            {
                return value.ToString("0.########", CultureInfo.InvariantCulture);
            }

            #endregion
        }
    }
}
