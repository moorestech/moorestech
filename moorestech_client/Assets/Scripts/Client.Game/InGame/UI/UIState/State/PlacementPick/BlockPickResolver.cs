using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Game.Block.Interface;

namespace Client.Game.InGame.UI.UIState.State.PlacementPick
{
    /// <summary>
    /// スポイトでピックしたブロックの選択可否と設置先を解決
    /// Resolves whether an eyedropped block is pickable and its placement target
    /// </summary>
    public class BlockPickResolver
    {
        private readonly PlacementTargetResolver _placementTargetResolver;

        public BlockPickResolver(PlacementTargetResolver placementTargetResolver)
        {
            _placementTargetResolver = placementTargetResolver;
        }

        // 拾ったブロックはそのまま手持ちにする（坂ベルトも坂のまま）
        // The picked block is held as-is, slopes included
        public bool TryResolvePickTarget(BlockId blockId, BlockDirection pickedDirection, out BlockPlacementTarget resolvedTarget)
        {
            resolvedTarget = null;

            // 未解放ブロックはピック不可。判定はビルドメニューと同じ解決点へ委ねる
            // Locked blocks are not pickable; the judgement is delegated to the same resolver the build menu uses
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            if (!_placementTargetResolver.IsBlockUnlocked(blockGuid)) return false;

            resolvedTarget = new BlockPlacementTarget(blockGuid, pickedDirection);
            return true;
        }
    }
}
