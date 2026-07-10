using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.UI.BuildMenu;
using Core.Master;
using Game.UnlockState;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// BuildMenuEntryCatalog の合成結果を web 配信用 DTO へ変換する
    /// Converts the BuildMenuEntryCatalog composition into web-delivery DTOs
    /// </summary>
    public static class BuildMenuEntryDtoFactory
    {
        public static List<BuildMenuEntryDto> CreateDtos(IGameUnlockStateData unlockState, ClientBlueprintLibrary blueprintLibrary)
        {
            var dtos = new List<BuildMenuEntryDto>();
            foreach (var entry in BuildMenuEntryCatalog.CreateEntries(unlockState, blueprintLibrary))
            {
                dtos.Add(new BuildMenuEntryDto
                {
                    EntryType = GetEntryTypeName(entry.EntryType),
                    EntryKey = GetEntryKey(entry),
                    // ラベルはツールチップ1行目（ブロック名等）を使う
                    // The label is the tooltip's first line (block name etc.)
                    Label = entry.ToolTipText.Split('\n')[0],
                    Tooltip = entry.ToolTipText,
                    IconUrl = CreateIconUrl(entry),
                });
            }
            return dtos;
        }

        // web契約の entryType 文字列（select アクションの照合と共有する）
        // The web-contract entryType string (shared with the select action's matching)
        public static string GetEntryTypeName(PlacementSelectionType type)
        {
            return type switch
            {
                PlacementSelectionType.Block => "block",
                PlacementSelectionType.TrainCar => "trainCar",
                PlacementSelectionType.ConnectTool => "connectTool",
                PlacementSelectionType.BlueprintCopy => "blueprintCopy",
                PlacementSelectionType.Blueprint => "blueprint",
                _ => type.ToString(),
            };
        }

        // 種別ごとの安定キー（配列indexは再配信でずれるため使わない）
        // Stable key per type (array indices shift across republishes, so they are never used)
        public static string GetEntryKey(BuildMenuEntry entry)
        {
            return entry.EntryType switch
            {
                PlacementSelectionType.Block => entry.BlockId.AsPrimitive().ToString(),
                PlacementSelectionType.TrainCar => entry.TrainCarGuid.ToString(),
                PlacementSelectionType.ConnectTool => entry.ConnectPlaceMode,
                PlacementSelectionType.Blueprint => entry.BlueprintName,
                _ => string.Empty,
            };
        }

        private static string CreateIconUrl(BuildMenuEntry entry)
        {
            switch (entry.EntryType)
            {
                case PlacementSelectionType.Block:
                    return $"{BlockIconEndpoint.PathPrefix}{entry.BlockId.AsPrimitive()}{BlockIconEndpoint.PathSuffix}";
                case PlacementSelectionType.TrainCar:
                    return $"{TrainCarIconEndpoint.PathPrefix}{entry.TrainCarGuid}{TrainCarIconEndpoint.PathSuffix}";
                case PlacementSelectionType.ConnectTool:
                {
                    // 接続ツールはマスタの IconItemGuid からアイテムアイコンを引く
                    // Connect tools resolve their icon from the master's IconItemGuid
                    var tool = MasterHolder.PlaceSystemMaster.PlaceSystem.Data.First(t => t.PlaceMode == entry.ConnectPlaceMode);
                    var itemId = MasterHolder.ItemMaster.GetItemId(tool.IconItemGuid.Value);
                    return $"{ItemIconEndpoint.PathPrefix}{itemId.AsPrimitive()}{ItemIconEndpoint.PathSuffix}";
                }
                default:
                    return null;
            }
        }
    }
}
