using System;
using System.Linq;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool
{
    /// <summary>
    /// 接続ツールが使うブロック・素材アイテムをマスタから選び出すセレクタ群
    /// Selectors that pick the blocks and material items used by connect tools out of the master
    /// </summary>
    public static class ConnectToolMasterSelector
    {
        // レール敷設の橋脚。TrainRailブロックのうち最も手前（sortPriority最小）のものを使う
        // The rail pier: the earliest (lowest sortPriority) TrainRail block
        public static BlockId? SelectRailPierBlockId()
        {
            return SelectFirstBlockIdOfType(BlockMasterElement.BlockTypeConst.TrainRail);
        }

        // 電線延長で建てる電柱。ElectricPoleブロックのうち最も手前（sortPriority最小）のものを使う
        // The pole raised by wire extension: the earliest (lowest sortPriority) ElectricPole block
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
