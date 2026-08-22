using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     1フレーム分の不可理由/案内行を保持
    ///     PlaceSystemがプッシュ、Presenterが表示
    ///     Holds one frame's block-reason/notice lines
    ///     Each PlaceSystem pushes them, the presenter shows them
    ///     行の順序はプッシュ順（地形干渉・重複 → 距離 → 素材 → 電線 → 案内）
    ///     Line order is push order (terrain/overlap → distance → materials → wire → notices)
    /// </summary>
    public class PlacementFeedback
    {
        private readonly List<TooltipLine> _lines = new();
        public IReadOnlyList<TooltipLine> Lines => _lines;

        public void Clear() => _lines.Clear();
        public void Add(TooltipLine line) => _lines.Add(line);

        public void AddBlockedByTerrain() => _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain));
        public void AddBlockedByExistingBlock() => _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock));
        public void AddTooFar() => _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceTooFar));
        public void AddWireShortage() => _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireNoWireItem));
        public void AddWireOutOfRangeNotice() => _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRangeNotice));

        // 消費電線が無いときは案内行を出さない（旧ラベルと同じ）
        // No notice line without wire consumption (same as the old label)
        public void AddWireCost(int totalWireCost)
        {
            if (totalWireCost <= 0) return;
            _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireCost, new[] { totalWireCost.ToString() }));
        }

        // 不足素材ごとに「名前 所持/必要」を1行積む(表示言語で解決)
        // One "name held/required" line per short material (name resolved in display language)
        public void AddMaterialShortages(IReadOnlyList<ConstructionMaterialShortage> shortages)
        {
            foreach (var shortage in shortages)
            {
                var itemGuid = MasterHolder.ItemMaster.GetItemMaster(shortage.ItemId).ItemGuid;
                var itemName = Localize.GetContent(ContentLocalizationKeys.ItemName(itemGuid));
                _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage, new[] { itemName, shortage.Held.ToString(), shortage.Required.ToString() }));
            }
        }
    }
}
