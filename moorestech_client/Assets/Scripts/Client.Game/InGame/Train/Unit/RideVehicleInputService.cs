using Client.Common;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Player;
using Client.Game.InGame.Train.View.Object;
using Client.Game.InGame.UI.UIState;
using UnityEngine;

namespace Client.Game.InGame.Train.Unit
{
    // GameScreenState 側で「E + 範囲内に列車」を検出し、TrainHUDScreen への遷移要求を作る入力サービス。
    // Input service that detects "E + train in range" from GameScreenState and builds a TrainHUDScreen transit.
    public sealed class RideVehicleInputService
    {
        // 乗車できる最大距離（メートル）。
        // Maximum distance (meters) at which a car can be boarded.
        private const float RideableDistance = 3.0f;
        
        private const int OverlapBufferSize = 16;

        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];

        // 範囲内に列車があり、Eが押されたら遷移
        // Transit if a train is in range and E is pressed.
        public bool TryGetInteractTransit(out UITransitContext context)
        {
            context = null;
            
            // TODO ほかプレイヤーが列車に乗っているかどうかをチェックする
            if (!UnityEngine.Input.GetKeyDown(KeyCode.E)) return false;
            if (!TryFindNearbyTrainCar(out var car)) return false;
            
            var container = UITransitContextContainer.Create(new RideTrainCarRequest(car.TrainCarInstanceId));
            
            context = new UITransitContext(UIStateEnum.TrainHUDScreen, container);
            return true;

            #region Internal

            // プレイヤー周囲を OverlapSphere で探索し、最寄りの TrainCarEntityObject を返す。
            // Searches around the player via OverlapSphere and returns the closest TrainCarEntityObject.
            bool TryFindNearbyTrainCar(out TrainCarEntityObject nearest)
            {
                nearest = null;
                var playerPos = PlayerSystemContainer.Instance.PlayerObjectController.Position;

                var hitCount = Physics.OverlapSphereNonAlloc(playerPos, RideableDistance, _overlapBuffer, LayerConst.BlockOnlyLayerMask);
                var nearestSqr = float.PositiveInfinity;
                for (var i = 0; i < hitCount; i++)
                {
                    var car = _overlapBuffer[i].GetComponentInParent<TrainCarEntityChildrenObject>();
                    if (car == null) continue;

                    var sqr = (car.transform.position - playerPos).sqrMagnitude;
                    if (sqr < nearestSqr)
                    {
                        nearestSqr = sqr;
                        nearest = car.TrainCarEntityObject;
                    }
                }

                return nearest != null;
            }

            #endregion
        }
    }
}
