using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.Train.View.Object;
using Cysharp.Threading.Tasks;
using Game.PlayerRiding.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Unit
{
    // E キーで最寄り車両への乗車要求 / 乗車中の降車要求を送る（仕様書セクション9）。
    // Sends a ride request to the nearest car / a dismount request while riding, on the E key.
    public sealed class TrainCarRidingInteractInput : ITickable
    {
        // 乗車できる最大距離（メートル）。
        // Maximum distance (meters) at which a car can be boarded.
        private const float RideableDistance = 3.0f;

        private readonly TrainCarRidingState _trainCarRidingState;
        private readonly TrainCarRidingPlayerController _ridingPlayerController;
        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;
        private bool _requestInFlight;

        public TrainCarRidingInteractInput(
            TrainCarRidingState trainCarRidingState,
            TrainCarRidingPlayerController ridingPlayerController,
            TrainCarObjectDatastore trainCarObjectDatastore)
        {
            _trainCarRidingState = trainCarRidingState;
            _ridingPlayerController = ridingPlayerController;
            _trainCarObjectDatastore = trainCarObjectDatastore;
        }

        public void Tick()
        {
            // 既存の乗車入力（WASD）が KeyCode 直読みなのに合わせ、E も直読みする。
            // Mirrors the existing direct-KeyCode read used by TrainCarRidingInputSender.
            if (!UnityEngine.Input.GetKeyDown(KeyCode.E)) return;
            if (_requestInFlight) return;

            if (_trainCarRidingState.IsRiding)
            {
                RequestDismount().Forget();
            }
            else
            {
                RequestRideNearestCar().Forget();
            }
        }

        private async UniTask RequestRideNearestCar()
        {
            var nearest = FindNearestCar();
            if (nearest == null) return;

            _requestInFlight = true;
            var target = RidableIdentifierMessagePack.CreateTrainCarMessage(nearest.TrainCarInstanceId.AsPrimitive());
            var response = await ClientContext.VanillaApi.Response.RideAction(RideActionType.Ride, target, CancellationToken.None);
            _requestInFlight = false;

            // 乗車成功なら要求元クライアント自身が座席へテレポートする（仕様書セクション5.1・9）。
            // On success the requesting client teleports itself to the seat.
            if (response != null && response.Result == RideActionResult.Success)
            {
                _ridingPlayerController.ApplyRide(nearest.TrainCarInstanceId);
            }
        }

        private async UniTask RequestDismount()
        {
            _requestInFlight = true;
            var response = await ClientContext.VanillaApi.Response.RideAction(RideActionType.Dismount, null, CancellationToken.None);
            _requestInFlight = false;

            // 降車成功なら座席から離れて操作可能に戻す。
            // On success, leave the seat and become controllable again.
            if (response != null && response.Result == RideActionResult.Success)
            {
                _ridingPlayerController.ApplyDismount();
            }
        }

        private TrainCarEntityObject FindNearestCar()
        {
            // プレイヤーから RideableDistance 内で最も近い車両を探す。
            // Find the closest car within RideableDistance of the player.
            var playerPos = PlayerSystemContainer.Instance.PlayerObjectController.Position;
            TrainCarEntityObject nearest = null;
            var nearestSqr = RideableDistance * RideableDistance;
            foreach (var car in _trainCarObjectDatastore.AllEntities)
            {
                if (car == null) continue;
                var sqr = (car.transform.position - playerPos).sqrMagnitude;
                if (sqr <= nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = car;
                }
            }
            return nearest;
        }
    }
}
