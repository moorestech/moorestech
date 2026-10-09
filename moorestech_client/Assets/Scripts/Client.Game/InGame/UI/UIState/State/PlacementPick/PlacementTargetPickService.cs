using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
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

            // 削除ツールと同じ照準規則（Block|ConnectionLine の最前面ヒット）で線・列車・ブロックへ振り分ける（壁の奥の線に奪われない）
            // Same aim rule as the delete tool (frontmost Block|ConnectionLine hit), dispatched to line, train car or block (a line behind a wall cannot steal it)
            var aimMask = LayerConst.BlockOnlyLayerMask | LayerConst.ConnectionLineOnlyLayerMask;
            if (!BlockClickDetectUtil.TryGetFrontmostSolidHit(aimMask, BlockClickDetectUtil.AimRayDistance, out var hit)) return false;
            var line = ConnectionLineDeleteTarget.FromCollider(hit.collider);
            if (line != null) return ConnectionLinePickResolver.TryResolve(line.ConnectToolGuid, _gameUnlockStateData, out pickedTarget);
            return TryPickTrainCar(out pickedTarget) || TryPickBlock(out pickedTarget);

            #region Internal

            bool TryPickTrainCar(out IPlacementTarget target)
            {
                target = null;

                // 列車のクリック用コライダーは車両ルートの子のため親方向にentityを解決する
                // Train click colliders sit under the car root, so resolve the entity toward parents
                var trainCar = hit.collider.GetComponentInParent<TrainCarEntityObject>();
                if (trainCar == null) return false;

                var trainCarGuid = trainCar.GetTrainCarMasterElement().TrainCarGuid;
                if (!TrainCarPickResolver.TryResolvePickTarget(trainCarGuid, _gameUnlockStateData, out var trainCarTarget)) return false;

                target = trainCarTarget;
                return true;
            }

            bool TryPickBlock(out IPlacementTarget target)
            {
                target = null;
                // 最前面ヒットの子要素からブロックを解決する
                // Resolve the block from the frontmost hit's children
                var child = hit.collider.gameObject.GetComponentInChildren<BlockGameObjectChild>();
                if (child == null) return false;
                var blockObject = child.BlockGameObject;
                if (!_blockPickResolver.TryResolvePickTarget(blockObject.BlockId, blockObject.BlockPosInfo.BlockDirection, out var blockTarget)) return false;

                target = blockTarget;
                return true;
            }

            #endregion
        }
    }
}
