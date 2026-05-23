using System.Threading;
using Client.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.Train.View.Object;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using Game.PlayerRiding.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.Train.Unit
{
    // GameScreen / TrainHUDScreen から呼ばれる乗車・降車入力サービス。
    // Ride/Dismount input service called from GameScreen / TrainHUDScreen.
    public sealed class RideVehicleInputService
    {
        // 乗車できる最大距離（メートル）。
        // Maximum distance (meters) at which a car can be boarded.
        private const float RideableDistance = 3.0f;
        // OverlapSphere の固定バッファ。同時に近接する車両は実質1〜数個。
        // Fixed buffer for OverlapSphere; only a few cars can ever be near the player at once.
        private const int OverlapBufferSize = 16;

        private readonly TrainCarRidingState _trainCarRidingState;
        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];

        public RideVehicleInputService(TrainCarRidingState trainCarRidingState)
        {
            _trainCarRidingState = trainCarRidingState;
        }

        // 範囲内に車両がありかつ E が押されたら、楽観的に乗車 State へ遷移する UITransitContext を返す。
        // Returns a TrainHUDScreen transit context when a car is in range and E was pressed (optimistic).
        public bool TryGetInteractTransit(out UITransitContext context)
        {
            context = null;
            if (_trainCarRidingState.IsRiding) return false;
            if (!UnityEngine.Input.GetKeyDown(KeyCode.E)) return false;
            if (!TryFindNearbyTrainCar(out var car)) return false;

            // 楽観的に IsRiding=true（seat=-1 のまま）にしてから RPC を fire-and-forget で送る。
            // RPC 失敗時は継続側で ClearRidingTrainCar し、TrainHUDScreenState が GameScreen へ戻す。
            // Optimistically mark riding (seat stays -1) and fire the ride RPC.
            // On failure, the continuation clears state and TrainHUDScreenState falls back to GameScreen.
            _trainCarRidingState.SetRidingTrainCar(car.TrainCarInstanceId, -1);
            SendRideRequestAsync(car).Forget();

            context = new UITransitContext(UIStateEnum.TrainHUDScreen);
            return true;
        }

        // 乗車中に E が押されたら降車 RPC を fire-and-forget で送る。
        // Sends a dismount RPC fire-and-forget when E is pressed while riding.
        public bool TryRequestDismount()
        {
            if (!UnityEngine.Input.GetKeyDown(KeyCode.E)) return false;

            SendDismountRequestAsync().Forget();
            return true;
        }

        private async UniTaskVoid SendRideRequestAsync(TrainCarEntityObject car)
        {
            var target = RidableIdentifierMessagePack.CreateTrainCarMessage(car.TrainCarInstanceId.AsPrimitive());
            var response = await ClientContext.VanillaApi.Response.RideAction(RideActionType.Ride, target, CancellationToken.None);

            // 成功なら座席番号付きで確定、失敗なら楽観的セットを戻す。
            // On success, finalize with the seat index; on failure, roll back the optimistic state.
            if (response != null && response.Result == RideActionResult.Success)
            {
                _trainCarRidingState.SetRidingTrainCar(car.TrainCarInstanceId, response.SeatIndex);
            }
            else
            {
                _trainCarRidingState.ClearRidingTrainCar();
            }
        }

        private async UniTaskVoid SendDismountRequestAsync()
        {
            var response = await ClientContext.VanillaApi.Response.RideAction(RideActionType.Dismount, null, CancellationToken.None);

            // 成功なら ClearRidingTrainCar し、TrainHUDScreenState が GameScreen へ戻す。
            // On success clear the state; TrainHUDScreenState transits back to GameScreen.
            if (response != null && response.Result == RideActionResult.Success)
            {
                _trainCarRidingState.ClearRidingTrainCar();
            }
        }

        // プレイヤー周囲を OverlapSphere で探索し、最寄りの TrainCarEntityObject を返す。
        // Searches around the player via OverlapSphere and returns the closest TrainCarEntityObject.
        private bool TryFindNearbyTrainCar(out TrainCarEntityObject nearest)
        {
            nearest = null;
            var playerPos = PlayerSystemContainer.Instance.PlayerObjectController.Position;

            // 車両は MeshCollider を Block レイヤーで持つ（TrainCarObjectFactory 参照）。
            // Train cars carry MeshColliders on the Block layer (see TrainCarObjectFactory).
            var hitCount = Physics.OverlapSphereNonAlloc(playerPos, RideableDistance, _overlapBuffer, LayerConst.BlockOnlyLayerMask);
            var nearestSqr = float.PositiveInfinity;
            for (var i = 0; i < hitCount; i++)
            {
                var car = _overlapBuffer[i].GetComponentInParent<TrainCarEntityObject>();
                if (car == null) continue;

                var sqr = (car.transform.position - playerPos).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = car;
                }
            }

            return nearest != null;
        }
    }
}
