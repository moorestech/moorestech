using Client.Game.InGame.Riding;
using Client.Game.InGame.Train.View.Object;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Player.StateController.State
{
    // 列車に乗車中のプレイヤーの管理を行うステート
    // A state that manages players riding trains.
    public class RidingPlayerState : IPlayerState
    {
        private static readonly Quaternion RidingLocalRotation = Quaternion.identity;

        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;
        
        private bool _isInitialized = false;
        private RidingPlayerStateContext _rideContext;
        
        public RidingPlayerState(TrainCarObjectDatastore trainCarObjectDatastore)
        {
            _trainCarObjectDatastore = trainCarObjectDatastore;
            trainCarObjectDatastore.OnInitializeComplete.Subscribe(_ =>
            {
                _isInitialized  = true;
                // 初期化終了かつコンテキストがあれば再適用する
                // Reapply if initialization is complete and there is a context.
                if (_rideContext != null)
                {
                    ApplyRidingContext(_rideContext);
                }
            });
        }

        public void OnEnter(IPlayerStateContext context)
        {
            _rideContext = context as RidingPlayerStateContext;
            ApplyRidingContext(_rideContext);
        }
        
        private void ApplyRidingContext(RidingPlayerStateContext rideContext)
        {
            // TrainCarの有効性をチェックし適用
            // Check the validity of the TrainCar and apply it.
            if (rideContext?.CurrentCarId != null && _trainCarObjectDatastore.TryGetEntity(rideContext.CurrentCarId, out var targetEntity))
            {
                var followTarget = ResolveSeatFollowTarget(targetEntity, rideContext.CurrentSeatIndex);
                
                var player = ResolvePlayerObjectController();
                player.SetRideFollowTarget(followTarget, Vector3.zero, RidingLocalRotation);
            }
            
            #region Internal
            
            static Transform ResolveSeatFollowTarget(TrainCarEntityObject targetEntity, int targetSeatIndex)
            {
                // Prefab上の座席markerを追従対象にする
                // Use the Prefab seat marker as the follow target
                var seatPositionResolver = targetEntity.GetComponent<SeatPositionResolver>();
                if (seatPositionResolver.TryGetSeatPosition(targetSeatIndex, out var seatTransform))
                {
                    return seatTransform;
                }

                // marker未設定時はroot原点へfallbackしてエラーを出す
                // Fall back to the root origin and log an error when the marker is missing
                Debug.LogError($"TrainCar SeatPosition not found. TrainCarInstanceId:{targetEntity.TrainCarInstanceId.AsPrimitive()} SeatIndex:{targetSeatIndex}");
                return targetEntity.transform;
            }
            
            #endregion
        }

        public void Tick()
        {
            // 初期化終了まではTrainCarオブジェクトが存在しないため何もしない
            // Do nothing until initialization is complete, as TrainCar objects do not exist yet.
            if (!_isInitialized) return;
            
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
                    
                    if (marker != null) return marker.transform.position;
                    return entity.transform.position;
                }

                return playerObject.Position;
            }

            #endregion
        }
        

        // TODOこのキャストをやめて必要なメソッドをIPlayerObjectControllerに入れる
        private static PlayerObjectController ResolvePlayerObjectController()
        {
            return PlayerSystemContainer.Instance?.PlayerObjectController as PlayerObjectController;
        }
    }
}
