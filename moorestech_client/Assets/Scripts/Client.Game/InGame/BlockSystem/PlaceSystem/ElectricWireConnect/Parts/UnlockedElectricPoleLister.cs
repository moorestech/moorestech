using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 解放済みの電柱ブロックをマスタから列挙する純粋クエリ。並び順が電線ツールの選択順そのものになる
    /// Pure query listing unlocked pole blocks from the master; the order is the wire tool's selection order
    /// </summary>
    public static class UnlockedElectricPoleLister
    {
        /// <summary>
        /// 解放済みElectricPoleブロックをSortPriority昇順、同値ならBlockGuid昇順で列挙する
        /// List unlocked ElectricPole blocks ascending by SortPriority, breaking ties by BlockGuid
        /// </summary>
        public static IReadOnlyList<BlockId> List(IGameUnlockStateData unlockState)
        {
            return MasterHolder.BlockMaster.Blocks.Data
                .Where(block => block.BlockType == BlockMasterElement.BlockTypeConst.ElectricPole)
                .Where(block => unlockState.BlockUnlockStateInfos.TryGetValue(block.BlockGuid, out var info) && info.IsUnlocked)
                .OrderBy(block => block.SortPriority ?? 0)
                .ThenBy(block => block.BlockGuid)
                .Select(block => MasterHolder.BlockMaster.GetBlockId(block.BlockGuid))
                .ToList();
        }
    }
}
