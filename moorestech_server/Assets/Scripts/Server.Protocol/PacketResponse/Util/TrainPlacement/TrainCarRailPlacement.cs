using System.Collections.Generic;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.TrainModule;
using Server.Util.MessagePack;
using static Server.Protocol.PacketResponse.PlaceTrainCarOnRailProtocol;

namespace Server.Protocol.PacketResponse.Util.TrainPlacement
{
    internal sealed class TrainCarRailPlacement
    {
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly TrainRailPositionManager _railPositionManager;
        private readonly TrainDiagramManager _diagramManager;

        internal TrainCarRailPlacement(ServiceProvider serviceProvider)
        {
            _railGraphDatastore = serviceProvider.GetService<IRailGraphDatastore>();
            _railPositionManager = serviceProvider.GetService<TrainRailPositionManager>();
            _diagramManager = serviceProvider.GetService<TrainDiagramManager>();
        }

        internal bool TryCreateTrainUnit(TrainCarMasterElement trainCarMaster, RailPositionSnapshotMessagePack railPositionSnapshot, out TrainUnit trainUnit, out PlaceTrainCarFailureType failureType)
        {
            trainUnit = null;
            failureType = PlaceTrainCarFailureType.InvalidRailPosition;

            // 期待する列車長とレール位置を検証する
            // Validate expected train length and rail position
            var expectedLength = TrainLengthConverter.ToRailUnits(trainCarMaster.Length);
            if (!TryRestoreRailPosition(railPositionSnapshot, expectedLength, out var railPosition, out failureType))
            {
                return false;
            }

            // 単一車両編成生成(コンテナ装着は自動)
            // Create a single-car train unit (container attached automatically).
            var trainCar = new TrainCar(trainCarMaster, true);
            trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, _railPositionManager, _diagramManager);
            return true;
        }

        private bool TryRestoreRailPosition(RailPositionSnapshotMessagePack snapshot, int expectedLength, out RailPosition position, out PlaceTrainCarFailureType failureType)
        {
            position = null;
            failureType = PlaceTrainCarFailureType.InvalidRailPosition;
            // スナップショットを検証する
            // Validate snapshot payload
            if (snapshot == null)
            {
                return false;
            }
            var saveData = snapshot.ToModel();
            if (!TryValidateSnapshot(saveData, expectedLength, out var validatedSnapshot, out failureType))
            {
                return false;
            }

            // RailPositionを復元する
            // Restore the rail position instance
            position = RailPositionFactory.Restore(validatedSnapshot, _railGraphDatastore);
            return position != null;
        }

        private bool TryValidateSnapshot(RailPositionSaveData snapshot, int expectedTrainLength, out RailPositionSaveData validatedSnapshot, out PlaceTrainCarFailureType failure)
        {
            validatedSnapshot = null;
            failure = PlaceTrainCarFailureType.InvalidRailPosition;
            // 入力と列車長を検証する
            // Validate inputs and train length
            if (snapshot == null || snapshot.RailSnapshot == null || snapshot.RailSnapshot.Count < 2)
            {
                return false;
            }
            if (snapshot.TrainLength != expectedTrainLength)
            {
                return false;
            }
            if (snapshot.DistanceToNextNode < 0)
            {
                return false;
            }

            // ノード列を解決する
            // Resolve node list from destinations
            var nodes = new List<IRailNode>(snapshot.RailSnapshot.Count);
            for (var i = 0; i < snapshot.RailSnapshot.Count; i++)
            {
                var node = _railGraphDatastore.ResolveRailNode(snapshot.RailSnapshot[i]);
                if (node == null)
                {
                    failure = PlaceTrainCarFailureType.RailNotFound;
                    return false;
                }
                nodes.Add(node);
            }

            // 距離と経路を検証する
            // Validate distances and path connectivity
            var totalDistance = 0;
            for (var i = 0; i < nodes.Count - 1; i++)
            {
                var segmentDistance = nodes[i + 1].GetDistanceToNode(nodes[i]);
                if (segmentDistance <= 0)
                {
                    return false;
                }
                if (i == 0 && segmentDistance < snapshot.DistanceToNextNode)
                {
                    return false;
                }
                totalDistance += segmentDistance;
            }

            var requiredDistance = snapshot.TrainLength + snapshot.DistanceToNextNode;
            if (totalDistance < requiredDistance)
            {
                return false;
            }

            validatedSnapshot = new RailPositionSaveData
            {
                TrainLength = expectedTrainLength,
                DistanceToNextNode = snapshot.DistanceToNextNode,
                RailSnapshot = snapshot.RailSnapshot
            };
            return true;
        }
    }
}
