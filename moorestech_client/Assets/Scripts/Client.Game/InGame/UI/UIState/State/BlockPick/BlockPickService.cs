using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Control;
using Client.Input;
using Game.UnlockState;

namespace Client.Game.InGame.UI.UIState.State.BlockPick
{
    /// <summary>
    /// ミドルクリックでカーソル下のブロックを設置ターゲットへ解決する
    /// Middle-click eyedropper: resolves the block under the cursor into a placement target
    /// </summary>
    public class BlockPickService
    {
        private readonly IGameUnlockStateData _gameUnlockStateData;

        public BlockPickService(IGameUnlockStateData gameUnlockStateData)
        {
            _gameUnlockStateData = gameUnlockStateData;
        }

        public bool TryPickBlockUnderCursor(out IPlacementTarget pickedTarget)
        {
            //TODO InputSystem対応
            if (!HybridInput.GetMouseButtonDown(2)) { pickedTarget = null; return false; }
            if (!BlockClickDetectUtil.TryGetCursorOnBlock(out var blockObject)) { pickedTarget = null; return false; }
            if (!BlockPickResolver.TryResolvePickTarget(blockObject.BlockId, _gameUnlockStateData, out var resolvedBlockId)) { pickedTarget = null; return false; }

            pickedTarget = new BlockPlacementTarget(resolvedBlockId, blockObject.BlockPosInfo.BlockDirection);
            return true;
        }
    }
}
