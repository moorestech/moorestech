using Core.Master;
using Game.Block.Interface.Extension;
using Game.UnlockState;

namespace Client.Game.InGame.UI.UIState.State.PlacementPick
{
    /// <summary>
    /// スポイトでピックしたブロックの選択可否と最終ブロックIDを解決する
    /// Resolves whether an eyedropped block is pickable and its final block id
    /// </summary>
    public static class BlockPickResolver
    {
        public static bool TryResolvePickTarget(BlockId rawBlockId, IGameUnlockStateData unlockState, out BlockId resolvedBlockId)
        {
            resolvedBlockId = rawBlockId;

            // ベルトファミリーはビルドメニューで選べる直線へ正規化する
            // Normalize belt-family members to the straight block available in the build menu
            if (BeltConveyorPlaceFamilyUtil.TryGetFamily(rawBlockId, out var family))
            {
                resolvedBlockId = family.StraightBlockId;
            }

            // 未解放ブロックはピック不可（スポイトで解放システムを迂回させない）
            // Locked blocks are not pickable; the eyedropper must not bypass the unlock system
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(resolvedBlockId).BlockGuid;
            return unlockState.BlockUnlockStateInfos.TryGetValue(blockGuid, out var info) && info.IsUnlocked;
        }
    }
}
