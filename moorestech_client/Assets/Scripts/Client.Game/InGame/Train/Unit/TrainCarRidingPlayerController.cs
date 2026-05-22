using Client.Game.InGame.Player;
using Client.Game.InGame.Train.View.Object;
using Game.Train.Unit;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Unit
{
    public sealed class TrainCarRidingPlayerController : ITickable, IInitializable, System.IDisposable
    {
        private static readonly Vector3 RidingLocalPosition = new(0f, 1f, 0f);
        private static readonly Quaternion RidingLocalRotation = Quaternion.identity;
        private static readonly Vector3 DismountOffset = new(0.75f, 0.5f, -1.5f);

        private readonly TrainCarRidingState _trainCarRidingState;
        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;
        private TrainCarInstanceId? _mountedTrainCarInstanceId;

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
                    playerObjectController.SetRideFollowTarget(entity.transform, RidingLocalPosition, RidingLocalRotation);
                }
                return;
            }

            ReleaseMountedPlayer(false);
        }

        public bool ApplyRide(TrainCarInstanceId targetCarId)
        {
            _trainCarRidingState.SetRidingTrainCar(targetCarId);

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

            var playerTransform = playerObjectController.transform;
            playerTransform.SetParent(null, true);
            playerObjectController.SetRideFollowTarget(entity.transform, RidingLocalPosition, RidingLocalRotation);
            playerObjectController.SetControllable(false);
            _mountedTrainCarInstanceId = targetCarId;
            return true;
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

            ApplyRide(pendingCarId.Value);
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
