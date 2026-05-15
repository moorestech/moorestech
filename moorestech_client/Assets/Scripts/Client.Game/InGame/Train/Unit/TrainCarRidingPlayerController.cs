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
                return;
            }

            var currentRidingTrainCarInstanceId = _trainCarRidingState.CurrentRidingTrainCarInstanceId;
            if (currentRidingTrainCarInstanceId.HasValue && currentRidingTrainCarInstanceId.Value == _mountedTrainCarInstanceId.Value)
            {
                var playerObjectController = ResolvePlayerObjectController();
                if (playerObjectController != null)
                {
                    var playerTransform = playerObjectController.transform;
                    playerTransform.localPosition = RidingLocalPosition;
                    playerTransform.localRotation = RidingLocalRotation;
                }
                return;
            }

            ReleaseMountedPlayer(false);
        }

        public bool ForceRide(TrainCarInstanceId targetCarId)
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
            playerTransform.SetParent(entity.transform, false);
            playerTransform.localPosition = RidingLocalPosition;
            playerTransform.localRotation = RidingLocalRotation;
            playerObjectController.SetControllable(false);
            _mountedTrainCarInstanceId = targetCarId;
            return true;
        }

        public void ForceDismount()
        {
            ReleaseMountedPlayer(true);
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
