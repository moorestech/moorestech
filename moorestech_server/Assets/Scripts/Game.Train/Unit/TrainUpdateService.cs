using System;
using System.Collections.Generic;
using Core.Update;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using UniRx;

namespace Game.Train.Unit
{
    public class TrainUpdateService
    {
        private readonly TrainDiagramManager _diagramManager;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly TrainManualControlService _trainManualControlService;

        // Train tick is aligned with the server game tick interval.
        private const double TickSeconds = GameUpdater.SecondsPerTick;
        public const double HashBroadcastIntervalSeconds = TickSeconds;
        private static readonly uint TrainUnitHashBroadcastIntervalTicks = Math.Max(4u, (uint)Math.Ceiling(HashBroadcastIntervalSeconds / TickSeconds));
        private uint _executedTick;
        private uint _tickSequenceId;

        private readonly Subject<HashStateEventData> _onHashEvent = new();
        private readonly Subject<(uint, IReadOnlyList<TrainTickDiffData>)> _onPreSimulationDiffEvent = new();
        private bool _trainAutoRunDebugEnabled;

        // Bind to required services and subscribe to update loop.
        public TrainUpdateService(
            TrainDiagramManager diagramManager,
            IRailGraphDatastore railGraphDatastore,
            ITrainUnitLookupDatastore trainUnitLookupDatastore,
            TrainManualControlService trainManualControlService)
        {
            _diagramManager = diagramManager;
            _railGraphDatastore = railGraphDatastore;
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
            _trainManualControlService = trainManualControlService;
            GameUpdater.UpdateObservable.Subscribe(_ => UpdateTrains());
        }

        public uint GetCurrentTick() => _executedTick;
        public uint NextTickSequenceId()
        {
            // Issue a monotonic id that represents train/rail event order.
            _tickSequenceId++;
            return _tickSequenceId;
        }
        public IObservable<HashStateEventData> OnHashEvent => _onHashEvent;
        public IObservable<(uint, IReadOnlyList<TrainTickDiffData>)> OnPreSimulationDiffEvent => _onPreSimulationDiffEvent;
        public bool IsTrainAutoRunDebugEnabled() => _trainAutoRunDebugEnabled;

        private void UpdateTrains()
        {
            // TrainUpdateService owns hash timing and emits dummy on skipped ticks.
            var hashState = BuildHashStateEventData(_executedTick);
            _onHashEvent.OnNext(hashState);

            _executedTick++;
            // Reset per-tick ordering counter when tick advances.
            _tickSequenceId = 0;
            _trainManualControlService.PrepareTrainsForTick(_executedTick);

            //simulation
            foreach (var trainUnit in _trainUnitLookupDatastore.GetRegisteredTrains())
            {
                trainUnit.Update();
            }

            NotifyPreSimulationDiff(_executedTick);

            // Snapshot creation is intentionally disabled here for now.
            return;

            #region Internal
            HashStateEventData BuildHashStateEventData(uint hashTick)
            {
                if (hashTick % TrainUnitHashBroadcastIntervalTicks != 0)
                {
                    return new HashStateEventData(hashTick, uint.MaxValue, uint.MaxValue);
                }

                var bundles = new List<TrainUnitSnapshotBundle>();
                foreach (var train in _trainUnitLookupDatastore.GetRegisteredTrains())
                {
                    bundles.Add(TrainUnitSnapshotFactory.CreateSnapshot(train));
                }
                var unitsHash = TrainUnitSnapshotHashCalculator.Compute(bundles);
                var railGraphHash = _railGraphDatastore.GetConnectNodesHash();
                return new HashStateEventData(hashTick, unitsHash, railGraphHash);
            }

            // Aggregate per-unit diffs and publish only changed units.
            void NotifyPreSimulationDiff(uint tick)
            {
                var diffs = new List<TrainTickDiffData>();
                foreach (var trainUnit in _trainUnitLookupDatastore.GetRegisteredTrains())
                {
                    var (masconLevelDiff, isNowDockingSpeedZero, approachingNodeIdDiff) = trainUnit.GetTickDiff();
                    if (!HasDiff(masconLevelDiff, isNowDockingSpeedZero, approachingNodeIdDiff))
                    {
                        continue;
                    }
                    diffs.Add(new TrainTickDiffData(trainUnit.TrainInstanceId, masconLevelDiff, isNowDockingSpeedZero, approachingNodeIdDiff));
                }
                // Emit the same-tick event even when diffs are empty as a simulation trigger.
                _onPreSimulationDiffEvent.OnNext((tick, diffs));
                
                bool HasDiff(int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeIdDiff)
                {
                    return masconLevelDiff != 0 || isNowDockingSpeedZero || approachingNodeIdDiff != -1;
                }
            }
            #endregion
        }

        public void ResetTick()
        {
            _executedTick = 0;
            _tickSequenceId = 0;
        }

        // TODO Remove this debug toggle after train auto-run setup is finalized.
        private const string TrainAutoRunOnArgument = "on";
        private const string TrainAutoRunOffArgument = "off";

        // Toggle auto-run for debugging.
        public void TurnOnorOffTrainAutoRun(IReadOnlyList<string> commandParts)
        {
            var mode = commandParts[1];
            if (string.Equals(mode, TrainAutoRunOnArgument, StringComparison.OrdinalIgnoreCase))
            {
                _trainAutoRunDebugEnabled = true;
                UnityEngine.Debug.Log("トグルスイッチ: Turning on auto-run for all trains.");
                AutoDiagramNodeAdditionExample();
                
                foreach (var train in _trainUnitLookupDatastore.GetRegisteredTrains())
                {
                    train.TurnOnAutoRun();
                }
            }

            if (string.Equals(mode, TrainAutoRunOffArgument, StringComparison.OrdinalIgnoreCase))
            {
                _trainAutoRunDebugEnabled = false;
                UnityEngine.Debug.Log("トグルスイッチ: Turning off auto-run for all trains.");
                foreach (var train in _trainUnitLookupDatastore.GetRegisteredTrains())
                {
                    train.TurnOffAutoRun();
                }
            }

            // Ignore unknown toggle arguments.
            return;

            #region Internal

            // Example helper that loads station exit nodes into every diagram.
            void AutoDiagramNodeAdditionExample()
            {
                // Collect station nodes for auto-run.
                var railNodes = _railGraphDatastore.GetRailNodes();
                var stationNodes = new List<RailNode>();
                for (int i = 0; i < railNodes.Count; i++)
                {
                    if (railNodes[i] != null)
                    {
                        // Add front exit station nodes to every train diagram.
                        if ((railNodes[i].StationRef.NodeSide == StationNodeSide.Back) && (railNodes[i].StationRef.NodeRole == StationNodeRole.Exit))
                        {
                            stationNodes.Add(railNodes[i]);
                        }
                    }
                }
                _diagramManager.ResetAndNotifyNodeAddition(stationNodes);
            }

            #endregion
        }

        public readonly struct TrainTickDiffData
        {
            public TrainInstanceId TrainInstanceId { get; }
            public int MasconLevelDiff { get; }
            public bool IsNowDockingSpeedZero { get; }
            public int ApproachingNodeIdDiff { get; }

            public TrainTickDiffData(TrainInstanceId trainInstanceId, int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeIdDiff)
            {
                TrainInstanceId = trainInstanceId;
                MasconLevelDiff = masconLevelDiff;
                IsNowDockingSpeedZero = isNowDockingSpeedZero;
                ApproachingNodeIdDiff = approachingNodeIdDiff;
            }
        }

        public readonly struct HashStateEventData
        {
            public uint Tick { get; }
            public uint UnitsHash { get; }
            public uint RailGraphHash { get; }

            public HashStateEventData(uint tick, uint unitsHash, uint railGraphHash)
            {
                Tick = tick;
                UnitsHash = unitsHash;
                RailGraphHash = railGraphHash;
            }
        }
    }
}
