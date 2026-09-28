using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Mooresmaster.Model.TrainModule;
using Server.Util.MessagePack;
using static Server.Protocol.PacketResponse.AttachTrainCarToUnitProtocol;

namespace Server.Protocol.PacketResponse.Util.TrainPlacement
{
    internal sealed class TrainCarAttachmentPlacement
    {
        private readonly IRailGraphDatastore _railGraphDatastore;

        internal TrainCarAttachmentPlacement(IRailGraphDatastore railGraphDatastore)
        {
            _railGraphDatastore = railGraphDatastore;
        }

        internal bool TryCreateCarAndRailPosition(
            TrainCarMasterElement trainCarMaster,
            AttachTrainCarToUnitRequestMessagePack request,
            out TrainCar attachingCar,
            out RailPosition attachingRailPosition,
            out AttachTrainCarFailureType failureType)
        {
            attachingCar = null;
            attachingRailPosition = null;
            failureType = AttachTrainCarFailureType.InvalidRailPosition;

            // レール位置を復元し長さ整合を検証する
            // Restore rail position and validate expected length
            var expectedLength = TrainLengthConverter.ToRailUnits(trainCarMaster.Length);
            if (!TryRestoreRailPosition(request.RailPosition, expectedLength, out attachingRailPosition, out failureType))
            {
                return false;
            }

            // 追加車両を作成する 自分の接続面が前、相手の接続面も前なら接続後の自分の向きはunitからみて反対向きになる->facingforwardはfalse
            // Create attaching car. Facing forward is false if both self and target connection sides are front, which means the attached car will face opposite direction from unit perspective after attachment.
            attachingCar = new TrainCar(trainCarMaster, request.AttachCarFacingForward ^ request.AttachToTargetTrainHead);

            // attachingRailPositionは接続先編成からみた方向に正規化する
            // Normalize attachingRailPosition so that its direction matches the connecting train unit. When the car is not facing forward relative to the unit, the physical front of the car lies on the opposite side, so we reverse the rail position to keep "head" and "rear" consistent with the target train's travel direction.
            if (!attachingCar.IsFacingForward)
                attachingRailPosition.Reverse();
            return true;
        }

        internal bool TryAttachToTargetTrain(TrainUnit targetTrain, TrainCar car, RailPosition railPosition, bool attachToTargetTrainHead)
        {
            if (targetTrain == null || car == null || railPosition == null)
            {
                return false;
            }

            // 指定された既存編成の先頭に連結する
            // Attach to target train head when requested
            if (attachToTargetTrainHead)
            {
                if (!targetTrain.RailPosition.GetHeadRailPosition().IsSamePositionAllowNodeOverlap(railPosition.GetRearRailPosition()))
                {
                    return false;
                }
                targetTrain.AttachCarToHead(car, railPosition);
                return true;
            }

            // 指定された既存編成の最後尾に連結する
            // Attach to target train rear when requested
            if (!railPosition.GetHeadRailPosition().IsSamePositionAllowNodeOverlap(targetTrain.RailPosition.GetRearRailPosition()))
            {
                return false;
            }
            targetTrain.AttachCarToRear(car, railPosition);
            return true;
        }

        private bool TryRestoreRailPosition(
            RailPositionSnapshotMessagePack snapshot,
            int expectedLength,
            out RailPosition railPosition,
            out AttachTrainCarFailureType failureType)
        {
            railPosition = null;
            failureType = AttachTrainCarFailureType.InvalidRailPosition;

            // スナップショットを変換・検証
            // Convert snapshot to save-data and validate
            if (snapshot == null)
            {
                return false;
            }
            var saveData = snapshot.ToModel();
            if (!TryValidateSnapshot(saveData, expectedLength, out var validatedSaveData, out failureType))
            {
                return false;
            }

            // 検証済みデータからRailPosition復元
            // Restore RailPosition from validated save-data
            railPosition = RailPositionFactory.Restore(validatedSaveData, _railGraphDatastore);
            return railPosition != null;
        }

        private bool TryValidateSnapshot(
            RailPositionSaveData snapshot,
            int expectedTrainLength,
            out RailPositionSaveData validatedSnapshot,
            out AttachTrainCarFailureType failureType)
        {
            validatedSnapshot = null;
            failureType = AttachTrainCarFailureType.InvalidRailPosition;

            // 入力値と列車長を検証する
            // Validate input payload and train length
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

            // ノード列を解決して経路整合性を検証する
            // Resolve rail nodes and validate path consistency
            var nodes = new System.Collections.Generic.List<IRailNode>(snapshot.RailSnapshot.Count);
            for (var i = 0; i < snapshot.RailSnapshot.Count; i++)
            {
                var node = _railGraphDatastore.ResolveRailNode(snapshot.RailSnapshot[i]);
                if (node == null)
                {
                    failureType = AttachTrainCarFailureType.RailNotFound;
                    return false;
                }
                nodes.Add(node);
            }

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
