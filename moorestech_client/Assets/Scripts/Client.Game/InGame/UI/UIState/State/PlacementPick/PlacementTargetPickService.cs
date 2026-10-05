using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Control;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Input;
using Game.UnlockState;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.PlacementPick
{
    /// <summary>
    /// ミドルクリックでカーソル下の設置物を設置ターゲットへ解決する
    /// Middle-click eyedropper: resolves the connection line, train car, or block under the cursor into a placement target
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

            // 接続線→列車→ブロックの順に解決する（線は細いため最優先で拾う）
            // Resolve connection line, then train car, then block (thin lines take priority)
            return TryPickConnectionLine(out pickedTarget) || TryPickTrainCar(out pickedTarget) || TryPickBlock(out pickedTarget);

            #region Internal

            bool TryPickConnectionLine(out IPlacementTarget target)
            {
                target = null;
                var aim = BlockClickDetectUtil.GetCursorOnConnectionLine();
                if (aim.Outcome != ConnectionLineAimOutcome.Found) return false;

                return ConnectionLinePickResolver.TryResolve(aim.Line.ConnectToolGuid, _gameUnlockStateData, out target);
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
                if (!_blockPickResolver.TryResolvePickTarget(blockObject.BlockId, blockObject.BlockPosInfo.BlockDirection, out var blockTarget)) return false;

                target = blockTarget;
                return true;
            }

            #endregion
        }
    }
}
