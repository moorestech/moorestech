using Client.Game.InGame.Train.View.Object;
using UnityEngine;

namespace Client.Game.InGame.Player.StateController.State
{
    // 乗車中の Player ステート。Tick で context (RidingPlayerStateContext) のプロパティを参照して
    // 現在乗るべき列車を取得し、車両 entity が揃ったタイミングで親付け＋カメラ追従を設定する。
    // OnExit で降車 pose を適用する。
    // Riding player state. Reads the current ride target from the context (RidingPlayerStateContext) every frame
    // and applies parenting + camera follow when the car entity is ready. OnExit warps to the dismount pose.
    public class RidingPlayerState : IPlayerState
    {
        private static readonly Quaternion RidingLocalRotation = Quaternion.identity;

        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;
        
        private RidingPlayerStateContext _rideContext;
        
        public RidingPlayerState(TrainCarObjectDatastore trainCarObjectDatastore)
        {
            _trainCarObjectDatastore = trainCarObjectDatastore;
        }

        public void OnEnter(IPlayerStateContext context)
        {
            _rideContext = context as RidingPlayerStateContext;
            
            if (_rideContext?.CurrentCarId != null && _trainCarObjectDatastore.TryGetEntity(_rideContext.CurrentCarId, out var targetEntity))
            {
                var seatPosition = ResolveSeatLocalPosition(targetEntity, _rideContext.CurrentSeatIndex);
                
                var player = ResolvePlayerObjectController();
                player.SetRideFollowTarget(targetEntity.transform, seatPosition, RidingLocalRotation);
            }
            
            #region Internal
            
            static Vector3 ResolveSeatLocalPosition(TrainCarEntityObject targetEntity, int targetSeatIndex)
            {
                var seats = targetEntity.TrainCarMasterElement.RidableSeats;
                if (seats == null || targetSeatIndex < 0 || seats.Length <= targetSeatIndex) return Vector3.zero;
                
                var seat = seats[targetSeatIndex];
                return new Vector3(seat.OffsetX, seat.OffsetY, seat.OffsetZ);
            }
            
            #endregion
        }

        public void Tick()
        {
            if (_rideContext == null) return;

            // 車両がすでに破棄されていたら乗車を解除する
            // If the car has already been destroyed, release the ride follow.
            if (!_trainCarObjectDatastore.TryGetEntity(_rideContext.CurrentCarId, out _))
            {
                var player = ResolvePlayerObjectController();
                player.ClearRideFollowTarget();
                _rideContext = null;
            }
            
        }

        public void OnExit()
        {
            // 降車を行うシステム
            var player = ResolvePlayerObjectController();
            var dismountPosition = ResolveDismountPose(player);
            
            player.ClearRideFollowTarget();
            player.SetPlayerPosition(dismountPosition);

            _rideContext = null;

            #region Internal

            Vector3 ResolveDismountPose(PlayerObjectController playerObject)
            {
                if (_rideContext != null && _trainCarObjectDatastore.TryGetEntity(_rideContext.CurrentCarId, out var entity))
                {
                    var marker = entity.GetComponentInChildren<TrainCarDismountPoint>(true);
                    
                    if (marker != null) return marker.Position;
                    return entity.transform.position;
                }

                return playerObject.Position;
            }

            #endregion
        }
        

        // TODOこのキャストをやめて
        private static PlayerObjectController ResolvePlayerObjectController()
        {
            return PlayerSystemContainer.Instance?.PlayerObjectController as PlayerObjectController;
        }
    }
}
