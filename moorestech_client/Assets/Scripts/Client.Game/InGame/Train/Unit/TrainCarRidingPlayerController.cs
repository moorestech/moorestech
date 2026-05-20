using Client.Game.InGame.Player;
using Client.Game.InGame.Train.View.Object;
using Game.Train.Unit;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Unit
{
    public sealed class TrainCarRidingPlayerController : ITickable, IInitializable, System.IDisposable
    {
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

            // 乗車状態が外部で解除されたら降車処理を行う
            // Release the player when the riding state has been cleared externally.
            var currentRidingTrainCarInstanceId = _trainCarRidingState.CurrentRidingTrainCarInstanceId;
            if (currentRidingTrainCarInstanceId.HasValue && currentRidingTrainCarInstanceId.Value == _mountedTrainCarInstanceId.Value)
            {
                return;
            }

            ReleaseMountedPlayer(false);
        }

        public bool ForceRide(TrainCarInstanceId targetCarId)
        {
            _trainCarRidingState.SetRidingTrainCar(targetCarId);

            if (!_trainCarObjectDatastore.TryGetEntity(targetCarId, out _))
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
