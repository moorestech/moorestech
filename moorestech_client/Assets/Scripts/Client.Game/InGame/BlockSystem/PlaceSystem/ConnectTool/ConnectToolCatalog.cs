using System;
using System.Linq;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.ConnectToolsModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool
{
    /// <summary>
    /// 接続ツール3種のenum変換と、ツール未選択経路のマスタ選択規則を定義する
    /// Defines enum conversion and no-tool-selected master-selection rules for the three connect tools
    /// </summary>
    public static class ConnectToolCatalog
    {
        // マスタのtoolType文字列をクライアントのルーティング用enumへ写す
        // Map the master's toolType string to the client routing enum
        public static ConnectToolType ToConnectToolType(string masterToolType)
        {
            return masterToolType switch
            {
                ConnectToolMasterElement.ToolTypeConst.electricWire => ConnectToolType.ElectricWireConnect,
                ConnectToolMasterElement.ToolTypeConst.rail => ConnectToolType.TrainRailConnect,
                ConnectToolMasterElement.ToolTypeConst.gearChain => ConnectToolType.GearChainPoleConnect,
                _ => throw new ArgumentOutOfRangeException(nameof(masterToolType), masterToolType, null),
            };
        }

        // クライアントのenumをマスタのtoolType文字列へ写す
        // Map the client enum to the master's toolType string
        public static string ToMasterToolType(ConnectToolType toolType)
        {
            return toolType switch
            {
                ConnectToolType.ElectricWireConnect => ConnectToolMasterElement.ToolTypeConst.electricWire,
                ConnectToolType.TrainRailConnect => ConnectToolMasterElement.ToolTypeConst.rail,
                ConnectToolType.GearChainPoleConnect => ConnectToolMasterElement.ToolTypeConst.gearChain,
                _ => throw new ArgumentOutOfRangeException(nameof(toolType), toolType, null),
            };
        }

        // ブロック設置延長など、ツール未選択の経路で使うconnectToolを種別からSortPriority最小で解決する
        // Resolve the connectTool for block-placement extend paths (no tool selected) by type, smallest SortPriority
        public static Guid ResolveDefaultConnectToolGuid(ConnectToolType toolType)
        {
            var masterToolType = ToMasterToolType(toolType);
            var element = MasterHolder.ConnectToolMaster.All
                .Where(e => e.ToolType == masterToolType)
                .OrderBy(e => e.SortPriority)
                .FirstOrDefault();
            return element?.ConnectToolGuid ?? Guid.Empty;
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
