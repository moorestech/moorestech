using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool
{
    /// <summary>
    /// 接続ツール3種の表示情報とマスタ選択規則を定義する
    /// Defines display information and master-selection rules for the three connect tools
    /// </summary>
    public static class ConnectToolCatalog
    {
        private static readonly IReadOnlyList<ConnectToolType> DisplayOrder = Array.AsReadOnly(new[]
        {
            ConnectToolType.TrainRailConnect,
            ConnectToolType.GearChainPoleConnect,
            ConnectToolType.ElectricWireConnect,
        });

        public static IReadOnlyList<ConnectToolType> GetDisplayOrder()
        {
            return DisplayOrder;
        }

        public static string GetDisplayName(ConnectToolType toolType)
        {
            return toolType switch
            {
                ConnectToolType.TrainRailConnect => "レール敷設",
                ConnectToolType.GearChainPoleConnect => "歯車チェーン接続",
                ConnectToolType.ElectricWireConnect => "電線接続",
                _ => throw new ArgumentOutOfRangeException(nameof(toolType), toolType, null),
            };
        }

        public static Guid? SelectIconItemGuid(ConnectToolType toolType)
        {
            return toolType switch
            {
                ConnectToolType.TrainRailConnect => SelectRailItemGuid(),
                ConnectToolType.GearChainPoleConnect => SelectGearChainItemGuid(),
                ConnectToolType.ElectricWireConnect => SelectElectricWireItemGuid(),
                _ => throw new ArgumentOutOfRangeException(nameof(toolType), toolType, null),
            };

            #region Internal

            Guid? SelectRailItemGuid()
            {
                var railItems = MasterHolder.TrainUnitMaster.GetRailItems();
                return railItems.Length == 0 ? null : railItems[0].ItemGuid;
            }

            Guid? SelectGearChainItemGuid()
            {
                var chainItems = MasterHolder.BlockMaster.Blocks.GearChainItems;
                return chainItems.Length == 0 ? null : chainItems[0].ItemGuid;
            }

            Guid? SelectElectricWireItemGuid()
            {
                var wireItems = MasterHolder.BlockMaster.Blocks.ElectricWireItems;
                return wireItems.Length == 0 ? null : wireItems[0].ItemGuid;
            }

            #endregion
        }

        // 空きスペース延長時の自動設置ブロックを解決する
        // Resolve the block auto-placed when extending into empty space
        public static bool TryGetPlaceBlock(ConnectToolType toolType, out BlockId blockId, out BlockMasterElement blockMaster)
        {
            var selectedBlockId = toolType switch
            {
                ConnectToolType.TrainRailConnect => SelectFirstBlockIdOfType(BlockMasterElement.BlockTypeConst.TrainRail),
                // 歯車チェーンは空き場所に設置しない
                // Gear chains are not placed in empty spaces
                ConnectToolType.GearChainPoleConnect => null,
                ConnectToolType.ElectricWireConnect => SelectFirstBlockIdOfType(BlockMasterElement.BlockTypeConst.ElectricPole),
                _ => throw new ArgumentOutOfRangeException(nameof(toolType), toolType, null),
            };
            if (selectedBlockId == null)
            {
                blockId = default;
                blockMaster = null;
                return false;
            }

            blockId = selectedBlockId.Value;
            blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            return true;

            #region Internal

            BlockId? SelectFirstBlockIdOfType(string blockType)
            {
                var blockMaster = MasterHolder.BlockMaster.Blocks.Data
                    .Where(block => block.BlockType == blockType)
                    .OrderBy(block => block.SortPriority ?? 0)
                    .ThenBy(block => block.Name)
                    .FirstOrDefault();

                return blockMaster == null ? null : MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid);
            }

            #endregion
        }
    }
}
