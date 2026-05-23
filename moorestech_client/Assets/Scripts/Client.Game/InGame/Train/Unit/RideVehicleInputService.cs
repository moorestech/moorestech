using Client.Common;
using Client.Game.InGame.Player;
using Client.Game.InGame.Train.View.Object;
using Client.Game.InGame.UI.UIState;
using UnityEngine;

namespace Client.Game.InGame.Train.Unit
{
    // GameScreenState 側で「E + 範囲内に列車」を検出し、TrainHUDScreen への遷移要求を作る入力サービス。
    // RPC 送信や乗降状態保持はここでは行わない（すべて TrainHUDScreenState が起点で実施）。
    // Input service that detects "E + train in range" from GameScreenState and builds a TrainHUDScreen transit.
    // RPC sends and ride-state persistence happen entirely in TrainHUDScreenState.
    public sealed class RideVehicleInputService
    {
        // 乗車できる最大距離（メートル）。
        // Maximum distance (meters) at which a car can be boarded.
        private const float RideableDistance = 3.0f;
        // OverlapSphere の固定バッファ。同時に近接する車両は実質1〜数個。
        // Fixed buffer for OverlapSphere; only a few cars can ever be near the player at once.
        private const int OverlapBufferSize = 16;

        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];

        // 範囲内に車両がありかつ E が押されたら、RideVehicleRequest を container に詰めた TrainHUDScreen transit を返す。
        // Returns a TrainHUDScreen transit (with the RideVehicleRequest in its container) when a car is in range and E was pressed.
        public bool TryGetInteractTransit(out UITransitContext context)
        {
            context = null;
            
            // TODO ほかプレイヤーが列車に乗っているかどうかをチェックする
            if (!UnityEngine.Input.GetKeyDown(KeyCode.E)) return false;
            if (!TryFindNearbyTrainCar(out var car)) return false;

            var container = new UITransitContextContainer();
            container.Set(new RideVehicleRequest(car.TrainCarInstanceId));
            context = new UITransitContext(UIStateEnum.TrainHUDScreen, container);
            return true;

            #region Internal

            // プレイヤー周囲を OverlapSphere で探索し、最寄りの TrainCarEntityObject を返す。
            // Searches around the player via OverlapSphere and returns the closest TrainCarEntityObject.
            bool TryFindNearbyTrainCar(out TrainCarEntityObject nearest)
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

            #endregion
        }
    }
}
