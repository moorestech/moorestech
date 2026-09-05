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
        private readonly BlockPickResolver _blockPickResolver;

        public PlacementTargetPickService(IGameUnlockStateData gameUnlockStateData, BlockPickResolver blockPickResolver)
        {
            _gameUnlockStateData = gameUnlockStateData;
            _blockPickResolver = blockPickResolver;
        }

        public bool TryPickTargetUnderCursor(out IPlacementTarget pickedTarget)
        {
            pickedTarget = null;

            //TODO InputSystem対応
            if (!HybridInput.GetMouseButtonDown(2)) return false;
            if (UiPointerHitTest.IsPointerOverAnyUi()) return false;
            // 左ドラッグ中はスポイトしない（遷移先でGetKeyUpが拾われ意図せず設置されるのを防ぐ）
            // Skip picking during a left-drag (the release would be consumed as a place click in the next state)
            if (HybridInput.GetMouseButton(0)) return false;

            // 電線→列車→ブロックの順に解決する（ワイヤー優先は電線ツールの切断判定と整合）
            // Resolve wire, then train car, then block (wire priority matches the wire tool's disconnect check)
            return TryPickElectricWire(out pickedTarget) || TryPickTrainCar(out pickedTarget) || TryPickBlock(out pickedTarget);

            #region Internal

            bool TryPickElectricWire(out IPlacementTarget target)
            {
                target = null;
                if (!BlockClickDetectUtil.TryGetCursorOnElectricWire(out _)) return false;

                // カーソル下の電線に対応するelectricWire connectToolを解決し、未解放ならスポイト自体を不成立にする
                // Resolve the electricWire connectTool under the cursor; if locked the eyedropper itself fails
                if (!ConnectToolCatalog.TryResolveDefaultConnectToolGuid(ConnectToolType.ElectricWireConnect, _gameUnlockStateData, out var wireToolGuid)) return false;

                target = new ConnectToolPlacementTarget(wireToolGuid);
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
                if (!_blockPickResolver.TryResolvePickTarget(blockObject.BlockId, blockObject.BlockPosInfo.BlockDirection, _gameUnlockStateData, out var blockTarget)) return false;

                target = blockTarget;
                return true;
            }

            #endregion
        }
    }
}
