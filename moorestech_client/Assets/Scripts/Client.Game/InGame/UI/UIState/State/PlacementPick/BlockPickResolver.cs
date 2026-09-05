using Core.Master;
using Game.Block.Interface.Extension;
using Game.UnlockState;

namespace Client.Game.InGame.UI.UIState.State.PlacementPick
{
    /// <summary>
    /// スポイトでピックしたブロックの選択可否を解決する
    /// Resolves whether an eyedropped block is pickable
    /// </summary>
    public static class BlockPickResolver
    {
        // 拾ったブロックはそのまま手持ちにする（坂ベルトも坂のまま）
        // The picked block is held as-is, slopes included
        public static bool IsPickable(BlockId blockId, IGameUnlockStateData unlockState)
        {
            // 未解放ブロックはピック不可（スポイトで解放システムを迂回させない）
            // Locked blocks are not pickable; the eyedropper must not bypass the unlock system
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            var unlockGuid = BeltConveyorPlaceFamilyUtil.ResolveUnlockBlockGuid(blockGuid);
            return unlockState.BlockUnlockStateInfos.TryGetValue(unlockGuid, out var info) && info.IsUnlocked;
        }
    }
}
