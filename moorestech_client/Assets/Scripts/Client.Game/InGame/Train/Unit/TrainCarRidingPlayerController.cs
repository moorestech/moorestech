using Client.Game.InGame.Player;
using Client.Game.InGame.Train.View.Object;
using Game.Train.Unit;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Unit
{
    public sealed class TrainCarRidingPlayerController : ITickable, IInitializable, System.IDisposable
    {
        private static readonly Quaternion RidingLocalRotation = Quaternion.identity;
        private static readonly Vector3 DismountOffset = new(0.75f, 0.5f, -1.5f);

        private readonly TrainCarRidingState _trainCarRidingState;
        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;
        private TrainCarInstanceId? _mountedTrainCarInstanceId;
        // 着席中の座席ローカル位置（座席マスタのオフセットから解決済み）。
        // Local seat position of the current mount, resolved from the seat master offset.
        private Vector3 _mountedSeatLocalPosition;

        public TrainCarRidingPlayerController(TrainCarRidingState trainCarRidingState, TrainCarObjectDatastore trainCarObjectDatastore)
        {
            _trainCarRidingState = trainCarRidingState;
            _trainCarObjectDatastore = trainCarObjectDatastore;
        }

        public void Initialize()
        {
            _trainCarObjectDatastore.TrainCarEntityRemoving += HandleRemovingTrainCar;
        }

        public void Dispose()
        {
            _trainCarObjectDatastore.TrainCarEntityRemoving -= HandleRemovingTrainCar;
        }

        public void Tick()
        {
            if (!_mountedTrainCarInstanceId.HasValue)
            {
                // 乗車状態はあるが未 parent（ログイン復帰）の場合、車両オブジェクト生成を待って parent する。
                // Riding state set but not yet parented (login restore): wait for the car object, then parent.
                TryMountPendingRidingState();
                return;
            }

            var currentRidingTrainCarInstanceId = _trainCarRidingState.CurrentRidingTrainCarInstanceId;
            if (currentRidingTrainCarInstanceId.HasValue && currentRidingTrainCarInstanceId.Value == _mountedTrainCarInstanceId.Value)
            {
                if (!_trainCarObjectDatastore.TryGetEntity(_mountedTrainCarInstanceId.Value, out var entity))
                {
                    ReleaseMountedPlayer(true);
                    return;
                }

                var playerObjectController = ResolvePlayerObjectController();
                if (playerObjectController != null)
                {
                    playerObjectController.SetRideFollowTarget(entity.transform, _mountedSeatLocalPosition, RidingLocalRotation);
                }
                return;
            }

            ReleaseMountedPlayer(false);
        }

        public bool ApplyRide(TrainCarInstanceId targetCarId, int seatIndex)
        {
            _trainCarRidingState.SetRidingTrainCar(targetCarId, seatIndex);

            if (!_trainCarObjectDatastore.TryGetEntity(targetCarId, out var entity))
            {
                _trainCarRidingState.ClearRidingTrainCar();
                return false;
            }

            var playerObjectController = ResolvePlayerObjectController();
            if (playerObjectController == null)
            {
                _trainCarRidingState.ClearRidingTrainCar();
                return false;
            }

            // 座席マスタのオフセットを解決し、車両オブジェクト相対で着席位置を決める（仕様書セクション9）。
            // Resolve the seat offset from master and seat the player relative to the car object.
            _mountedSeatLocalPosition = ResolveSeatLocalPosition(entity, seatIndex);

            var playerTransform = playerObjectController.transform;
            playerTransform.SetParent(null, true);
            playerObjectController.SetRideFollowTarget(entity.transform, _mountedSeatLocalPosition, RidingLocalRotation);
            playerObjectController.SetControllable(false);
            _mountedTrainCarInstanceId = targetCarId;
            return true;
        }

        // 座席マスタ（ridableSeats）の seatIndex 番目のオフセットを返す。マスタが無い・範囲外なら原点に着席する。
        // Returns the offset of seat seatIndex from the seat master (ridableSeats); falls back to the origin when missing or out of range.
        private static Vector3 ResolveSeatLocalPosition(TrainCarEntityObject entity, int seatIndex)
        {
            var seats = entity.TrainCarMasterElement.RidableSeats;
            if (seats == null || seatIndex < 0 || seats.Length <= seatIndex)
            {
                return Vector3.zero;
            }

            var seat = seats[seatIndex];
            return new Vector3((float)seat.OffsetX, (float)seat.OffsetY, (float)seat.OffsetZ);
        }

        public void ApplyDismount()
        {
            ReleaseMountedPlayer(true);
        }

        // 乗車状態が復元済み（IsRiding）かつ未 parent のとき、対象車両が生成され次第 parent する。
        // When riding state is restored but not yet parented, parent the player once the target car object exists.
        private void TryMountPendingRidingState()
        {
            var pendingCarId = _trainCarRidingState.CurrentRidingTrainCarInstanceId;
            if (!pendingCarId.HasValue)
            {
                return;
            }

            if (!_trainCarObjectDatastore.TryGetEntity(pendingCarId.Value, out _))
            {
                return;
            }

            ApplyRide(pendingCarId.Value, _trainCarRidingState.CurrentSeatIndex);
        }

        public void HandleRemovingTrainCar(TrainCarInstanceId trainCarInstanceId)
        {
            if (!_mountedTrainCarInstanceId.HasValue || _mountedTrainCarInstanceId.Value != trainCarInstanceId)
            {
                return;
            }

            ReleaseMountedPlayer(true);
        }

        private void ReleaseMountedPlayer(bool clearRidingState)
        {
            var playerObjectController = ResolvePlayerObjectController();
            if (playerObjectController != null)
            {
                playerObjectController.ClearRideFollowTarget();
                if (_mountedTrainCarInstanceId.HasValue)
                {
                    var playerTransform = playerObjectController.transform;
                    var worldPosition = playerTransform.position;
                    var worldRotation = playerTransform.rotation;

                    playerTransform.SetParent(null, true);
                    playerObjectController.SetPlayerPosition(worldPosition + worldRotation * DismountOffset);
                    playerTransform.rotation = worldRotation;
                }

                playerObjectController.SetControllable(true);
            }

            _mountedTrainCarInstanceId = null;
            if (clearRidingState)
            {
                _trainCarRidingState.ClearRidingTrainCar();
            }
        }

        private static PlayerObjectController ResolvePlayerObjectController()
        {
            return PlayerSystemContainer.Instance?.PlayerObjectController as PlayerObjectController;
        }
    }
}
