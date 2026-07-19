using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Control;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Input;
using Game.UnlockState;
using Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool;

namespace Client.Game.InGame.UI.UIState.State.PlacementPick
{
    /// <summary>
    /// ミドルクリックでカーソル下の設置物を設置ターゲットへ解決する
    /// Middle-click eyedropper: resolves the wire, train car, or block under the cursor into a placement target
    /// </summary>
    public class PlacementTargetPickService
    {
        private readonly IGameUnlockStateData _gameUnlockStateData;

        public PlacementTargetPickService(IGameUnlockStateData gameUnlockStateData)
        {
            _gameUnlockStateData = gameUnlockStateData;
        }

        public bool TryPickTargetUnderCursor(out IPlacementTarget pickedTarget)
        {
            pickedTarget = null;

            //TODO InputSystem対応
            if (!HybridInput.GetMouseButtonDown(2)) return false;
            if (UiPointerHitTest.IsPointerOverAnyUi()) return false;

            // 電線→列車→ブロックの順に解決する（ワイヤー優先は電線ツールの切断判定と整合）
            // Resolve wire, then train car, then block (wire priority matches the wire tool's disconnect check)
            return TryPickElectricWire(out pickedTarget) || TryPickTrainCar(out pickedTarget) || TryPickBlock(out pickedTarget);

            #region Internal

            bool TryPickElectricWire(out IPlacementTarget target)
            {
                target = null;
                if (!BlockClickDetectUtil.TryGetCursorOnElectricWire(out _)) return false;

                // カーソル下の電線に対応するelectricWire connectToolを解決する
                // Resolve the electricWire connectTool corresponding to the wire under the cursor
                target = new ConnectToolPlacementTarget(ConnectToolCatalog.ResolveDefaultConnectToolGuid(ConnectToolType.ElectricWireConnect));
                return true;
            }

            bool TryPickTrainCar(out IPlacementTarget target)
            {
                target = null;

                // 列車のクリック用コライダーは車両ルートの子のため親方向にentityを解決する
                // Train click colliders sit under the car root, so resolve the entity toward parents
                if (!BlockClickDetectUtil.TryGetCursorOnComponentInParent(out TrainCarEntityObject trainCar)) return false;

                var trainCarGuid = trainCar.GetTrainCarMasterElement().TrainCarGuid;
                if (!TrainCarPickResolver.TryResolvePickTarget(trainCarGuid, _gameUnlockStateData, out var trainCarTarget)) return false;

                target = trainCarTarget;
                return true;
            }

            bool TryPickBlock(out IPlacementTarget target)
            {
                target = null;
                if (!BlockClickDetectUtil.TryGetCursorOnBlock(out var blockObject)) return false;
                if (!BlockPickResolver.TryResolvePickTarget(blockObject.BlockId, _gameUnlockStateData, out var resolvedBlockId)) return false;

                target = new BlockPlacementTarget(resolvedBlockId, blockObject.BlockPosInfo.BlockDirection);
                return true;
            }

            #endregion
        }
    }
}
