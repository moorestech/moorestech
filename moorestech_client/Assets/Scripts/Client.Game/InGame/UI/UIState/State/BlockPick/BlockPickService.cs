using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control;
using Client.Input;
using Game.UnlockState;

namespace Client.Game.InGame.UI.UIState.State.BlockPick
{
    /// <summary>
    /// ミドルクリックでカーソル下のブロックをピックし設置選択状態へ反映する
    /// Middle-click eyedropper: picks the block under the cursor into the placement selection
    /// </summary>
    public class BlockPickService
    {
        private readonly PlacementSelection _placementSelection;
        private readonly IGameUnlockStateData _gameUnlockStateData;

        public BlockPickService(PlacementSelection placementSelection, IGameUnlockStateData gameUnlockStateData)
        {
            _placementSelection = placementSelection;
            _gameUnlockStateData = gameUnlockStateData;
        }

        public bool TryPickBlockUnderCursor()
        {
            //TODO InputSystem対応
            if (!HybridInput.GetMouseButtonDown(2)) return false;
            if (!BlockClickDetectUtil.TryGetCursorOnBlock(out var blockObject)) return false;
            if (!BlockPickResolver.TryResolvePickTarget(blockObject.BlockId, _gameUnlockStateData, out var resolvedBlockId)) return false;

            // ブロック種類と設置向きをまとめて選択状態へ反映する
            // Apply both the block type and its placed direction to the selection
            _placementSelection.SetSelectedBlock(resolvedBlockId, blockObject.BlockPosInfo.BlockDirection);
            return true;
        }
    }
}
