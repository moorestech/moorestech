using System;
using System.Linq;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool
{
    /// <summary>
    /// 接続ツールのブロック・素材をマスタから選ぶセレクタ群
    /// Selectors picking connect-tool blocks and materials from the master
    /// </summary>
    public static class ConnectToolMasterSelector
    {
        // 橋脚はsortPriority最小のTrainRailブロック
        // The pier is the lowest-sortPriority TrainRail block
        public static BlockId? SelectRailPierBlockId()
        {
            return SelectFirstBlockIdOfType(BlockMasterElement.BlockTypeConst.TrainRail);
        }

        // 電柱はsortPriority最小のElectricPoleブロック
        // The pole is the lowest-sortPriority ElectricPole block
        public static BlockId? SelectElectricPoleBlockId()
        {
            return SelectFirstBlockIdOfType(BlockMasterElement.BlockTypeConst.ElectricPole);
        }

        // 歯車チェーン接続は空きスペースへブロックを建てないため設置ブロックを持たない
        // Gear chain connect never raises a block into empty space, so it has no place block
        public static BlockId? SelectNoPlaceBlock()
        {
            return null;
        }

        public static Guid? SelectRailItemGuid()
        {
            var railItems = MasterHolder.TrainUnitMaster.GetRailItems();
            return railItems.Length == 0 ? null : railItems[0].ItemGuid;
        }

        public static Guid? SelectGearChainItemGuid()
        {
            var chainItems = MasterHolder.BlockMaster.Blocks.GearChainItems;
            return chainItems.Length == 0 ? null : chainItems[0].ItemGuid;
        }

        public static Guid? SelectElectricWireItemGuid()
        {
            var wireItems = MasterHolder.BlockMaster.Blocks.ElectricWireItems;
            return wireItems.Length == 0 ? null : wireItems[0].ItemGuid;
        }

        private static BlockId? SelectFirstBlockIdOfType(string blockType)
        {
            var blockMaster = MasterHolder.BlockMaster.Blocks.Data
                .Where(block => block.BlockType == blockType)
                .OrderBy(block => block.SortPriority ?? 0)
                .ThenBy(block => block.Name)
                .FirstOrDefault();

            return blockMaster == null ? null : MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid);
        }
    }
}
