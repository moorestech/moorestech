using System;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ConnectTool
{
    /// <summary>
    /// 接続ツール3種のコード定義カタログ（旧placeSystemマスタの置換）
    /// Code-defined catalog of the three connect tools (replaces the placeSystem master)
    /// </summary>
    public static class ConnectToolCatalog
    {
        // 定義順がそのままビルドメニューの表示順になる
        // The definition order is the build-menu display order
        private static readonly List<ConnectToolDefinition> Definitions = new()
        {
            new ConnectToolDefinition(
                ConnectToolType.TrainRailConnect,
                "レール敷設",
                ConnectToolMasterSelector.SelectRailItemGuid,
                ConnectToolMasterSelector.SelectRailPierBlockId),
            new ConnectToolDefinition(
                ConnectToolType.GearChainPoleConnect,
                "歯車チェーン接続",
                ConnectToolMasterSelector.SelectGearChainItemGuid,
                ConnectToolMasterSelector.SelectNoPlaceBlock),
            new ConnectToolDefinition(
                ConnectToolType.ElectricWireConnect,
                "電線接続",
                ConnectToolMasterSelector.SelectElectricWireItemGuid,
                ConnectToolMasterSelector.SelectElectricPoleBlockId),
        };

        public static IReadOnlyList<ConnectToolDefinition> GetDefinitionsInDisplayOrder()
        {
            return Definitions;
        }

        public static ConnectToolDefinition GetDefinition(ConnectToolType toolType)
        {
            foreach (var definition in Definitions)
            {
                if (definition.ToolType == toolType) return definition;
            }

            throw new InvalidOperationException($"ConnectToolDefinition not found. ToolType:{toolType}");
        }
    }
}
