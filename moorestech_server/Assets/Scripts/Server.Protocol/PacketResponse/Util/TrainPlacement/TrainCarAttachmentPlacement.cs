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

            #region Internal

            bool TryRestoreRailPosition(RailPositionSnapshotMessagePack snapshot, int restoreExpectedLength,
                out RailPosition position, out AttachTrainCarFailureType restoreFailureType)
            {
                var valid = RailPositionSnapshotValidator.TryValidate(snapshot, restoreExpectedLength, _railGraphDatastore,
                    out position, out var railNotFound);
                restoreFailureType = railNotFound ? AttachTrainCarFailureType.RailNotFound : AttachTrainCarFailureType.InvalidRailPosition;
                return valid;
            }

            #endregion
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
    }
}
