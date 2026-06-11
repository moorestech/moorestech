using Client.Game.InGame.Player.StateController;
using Client.Game.InGame.Train.View.Object.Core;
using Game.PlayerRiding.Interface;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Game.InGame.Riding
{
    public sealed class TrainCarRideFollowTargetResolver : IRideFollowTargetResolver
    {
        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;

        public TrainCarRideFollowTargetResolver(TrainCarObjectDatastore trainCarObjectDatastore)
        {
            // train car の view object 解決だけをこの resolver に閉じ込める
            // Keep train-car view object resolution isolated inside this resolver
            _trainCarObjectDatastore = trainCarObjectDatastore;
        }

        public bool TryResolveFollowTarget(RidingPlayerStateContext context, out Transform followTarget)
        {
            followTarget = null;
            if (!TryResolveTrainCarEntity(context, out var targetEntity))
            {
                return false;
            }

            // 座席 marker が見つかればそこへ追従し、未設定なら車両 root へ fallback する
            // Follow the seat marker when available and fall back to the car root when it is missing
            var seatPositionResolver = targetEntity.GetComponent<SeatPositionResolver>();
            if (seatPositionResolver.TryGetSeatPosition(context.GetSeatIndex(), out var seatTransform))
            {
                followTarget = seatTransform;
                return true;
            }

            Debug.LogError($"TrainCar SeatPosition not found. TrainCarInstanceId:{targetEntity.TrainCarInstanceId.AsPrimitive()} SeatIndex:{context.GetSeatIndex()}");
            followTarget = targetEntity.transform;
            return true;
        }

        public bool Exists(RidingPlayerStateContext context)
        {
            return TryResolveTrainCarEntity(context, out _);
        }

        public Vector3 ResolveDismountPosition(RidingPlayerStateContext context, Vector3 fallbackPosition)
        {
            if (!TryResolveTrainCarEntity(context, out var targetEntity))
            {
                return fallbackPosition;
            }

            // 降車 marker があれば優先し、なければ車両 root を降車位置にする
            // Prefer the dismount marker and use the car root when the marker is missing
            var marker = targetEntity.GetComponentInChildren<TrainCarDismountPoint>(true);
            if (marker != null)
            {
                return marker.transform.position;
            }
            return targetEntity.transform.position;
        }

        private bool TryResolveTrainCarEntity(RidingPlayerStateContext context, out TrainCarEntityObject targetEntity)
        {
            targetEntity = null;
            if (context == null || !context.TryGetTarget(out var target))
            {
                return false;
            }

            // この実装では TrainCar ridable だけを処理する
            // This implementation handles only TrainCar ridables
            if (target.RidableType != RidableType.TrainCar)
            {
                return false;
            }
            var trainCarInstanceId = new TrainCarInstanceId(target.TrainCarInstanceId);
            return _trainCarObjectDatastore.TryGetEntity(trainCarInstanceId, out targetEntity);
        }
    }
}
