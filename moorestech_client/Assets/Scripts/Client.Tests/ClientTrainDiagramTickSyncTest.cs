using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.SaveLoad;
using Game.Train.Unit;
using NUnit.Framework;
using TrainDiagramType = Game.Train.Diagram.TrainDiagram;

namespace Client.Tests
{
    public class ClientTrainDiagramTickSyncTest
    {
        [Test]
        public void TickDockedDepartureConditions_AdvancesWaitTicksAndCanReset()
        {
            // ドッキング中の待機条件がtickで進み、リセットで初期化できることを確認する。
            // Verify docked wait conditions advance by tick and can be reset.
            var entryId = Guid.NewGuid();
            var diagram = CreateDiagram(
                currentIndex: 0,
                entries: new[]
                {
                    CreateEntry(entryId, TrainDiagramType.DepartureConditionType.WaitForTicks, 3, 3),
                });

            Assert.IsFalse(diagram.CanCurrentEntryDepart());

            diagram.TickDockedDepartureConditions(true);
            diagram.TickDockedDepartureConditions(true);
            Assert.IsFalse(diagram.CanCurrentEntryDepart());

            diagram.TickDockedDepartureConditions(true);
            Assert.IsTrue(diagram.CanCurrentEntryDepart());

            diagram.ResetCurrentEntryDepartureConditions();
            Assert.IsFalse(diagram.CanCurrentEntryDepart());

            #region Internal

            ClientTrainDiagram CreateDiagram(int currentIndex, TrainDiagramEntrySnapshot[] entries)
            {
                // ノード解決を使わないテストなので、最小実装のproviderを渡す。
                // Provide a minimal provider because node resolution is out of scope here.
                var snapshot = new TrainDiagramSnapshot(currentIndex, entries);
                return new ClientTrainDiagram(snapshot, new NoOpRailGraphProvider());
            }

            TrainDiagramEntrySnapshot CreateEntry(
                Guid currentEntryId,
                TrainDiagramType.DepartureConditionType? departureCondition,
                int? waitForTicksInitial,
                int? waitForTicksRemaining)
            {
                var conditions = departureCondition.HasValue
                    ? new[] { departureCondition.Value }
                    : Array.Empty<TrainDiagramType.DepartureConditionType>();

                return new TrainDiagramEntrySnapshot(
                    currentEntryId,
                    ConnectionDestination.Default,
                    conditions,
                    waitForTicksInitial,
                    waitForTicksRemaining);
            }

            #endregion
        }

        [Test]
        public void UpdateIndexByEntryId_AlignsCurrentEntryToEventEntry()
        {
            // 受信したentryIdで現在インデックスを合わせられることを確認する。
            // Verify current index is aligned by the received entryId.
            var firstEntryId = Guid.NewGuid();
            var secondEntryId = Guid.NewGuid();
            var diagram = CreateDiagram(
                currentIndex: 0,
                entries: new[]
                {
                    CreateEntry(firstEntryId, null, null, null),
                    CreateEntry(secondEntryId, null, null, null),
                });

            Assert.IsTrue(diagram.UpdateIndexByEntryId(secondEntryId));
            Assert.IsTrue(diagram.TryGetCurrentEntry(out var currentEntry));
            Assert.AreEqual(secondEntryId, currentEntry.EntryId);

            #region Internal

            ClientTrainDiagram CreateDiagram(int currentIndex, TrainDiagramEntrySnapshot[] entries)
            {
                // ノード解決を使わないテストなので、最小実装のproviderを渡す。
                // Provide a minimal provider because node resolution is out of scope here.
                var snapshot = new TrainDiagramSnapshot(currentIndex, entries);
                return new ClientTrainDiagram(snapshot, new NoOpRailGraphProvider());
            }

            TrainDiagramEntrySnapshot CreateEntry(
                Guid currentEntryId,
                TrainDiagramType.DepartureConditionType? departureCondition,
                int? waitForTicksInitial,
                int? waitForTicksRemaining)
            {
                var conditions = departureCondition.HasValue
                    ? new[] { departureCondition.Value }
                    : Array.Empty<TrainDiagramType.DepartureConditionType>();

                return new TrainDiagramEntrySnapshot(
                    currentEntryId,
                    ConnectionDestination.Default,
                    conditions,
                    waitForTicksInitial,
                    waitForTicksRemaining);
            }

            #endregion
        }

        #if UNITY_EDITOR
        private sealed class NoOpRailGraphProvider : IRailGraphProvider
        {
            public IRailNode ResolveRailNode(ConnectionDestination destination)
            {
                return null;
            }

            public IReadOnlyList<IRailNode> FindShortestPath(IRailNode start, IRailNode end)
            {
                return Array.Empty<IRailNode>();
            }

            public int GetDistance(IRailNode start, IRailNode end, bool useFindPath)
            {
                return -1;
            }
        }
        #endif
    }
}
