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

        private bool TryRestoreRailPosition(RailPositionSnapshotMessagePack snapshot, int expectedLength,
            out RailPosition position, out PlaceTrainCarFailureType failureType)
        {
            var valid = RailPositionSnapshotValidator.TryValidate(snapshot, expectedLength, _railGraphDatastore,
                out position, out var railNotFound);
            failureType = railNotFound ? PlaceTrainCarFailureType.RailNotFound : PlaceTrainCarFailureType.InvalidRailPosition;
            return valid;
        }
    }
}
