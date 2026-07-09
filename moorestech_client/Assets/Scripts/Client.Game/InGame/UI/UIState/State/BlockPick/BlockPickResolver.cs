using Core.Master;
using Game.Block.Interface.Extension;
using Game.UnlockState;

namespace Client.Game.InGame.UI.UIState.State.BlockPick
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

            // ベルトファミリーは代表ブロックへ変換（ビルドメニューの隠しバリアント除外と整合）
            // Belt family members resolve to the representative block, matching the menu's hidden-variant exclusion
            if (BeltConveyorPlaceFamilyUtil.TryGetFamily(rawBlockId, out var beltParam))
            {
                resolvedBlockId = BeltConveyorPlaceFamilyUtil.GetRepresentativeBlockId(beltParam);
            }

            // 未解放ブロックはピック不可（スポイトで解放システムを迂回させない）
            // Locked blocks are not pickable; the eyedropper must not bypass the unlock system
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(resolvedBlockId).BlockGuid;
            return unlockState.BlockUnlockStateInfos.TryGetValue(blockGuid, out var info) && info.IsUnlocked;
        }
    }
}
