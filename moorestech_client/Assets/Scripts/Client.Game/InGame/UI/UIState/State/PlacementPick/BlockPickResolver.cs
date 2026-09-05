using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
using Game.PlacementTarget;
using Game.UnlockState;

namespace Client.Game.InGame.UI.UIState.State.PlacementPick
{
    /// <summary>
    /// スポイトでピックしたブロックの選択可否と設置先を解決
    /// Resolves whether an eyedropped block is pickable and its placement target
    /// </summary>
    public class BlockPickResolver
    {
        private readonly PlacementTargetCatalog _placementTargetCatalog;

        public BlockPickResolver(PlacementTargetCatalog placementTargetCatalog)
        {
            _placementTargetCatalog = placementTargetCatalog;
        }

        // 拾ったブロックはそのまま手持ちにする（坂ベルトも坂のまま）
        // The picked block is held as-is, slopes included
        public bool TryResolvePickTarget(BlockId blockId, BlockDirection pickedDirection, IGameUnlockStateData unlockState, out BlockPlacementTarget resolvedTarget)
        {
            resolvedTarget = null;

            // 未解放ブロックはピック不可。判定はビルドメニューと同じカタログへ委ねる
            // Locked blocks are not pickable; the judgement is delegated to the same catalog the build menu uses
            var showAllPlaceable = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            if (!_placementTargetCatalog.IsBlockUnlocked(blockGuid, unlockState, showAllPlaceable)) return false;

            resolvedTarget = new BlockPlacementTarget(blockGuid, pickedDirection);
            return true;
        }
    }
}
