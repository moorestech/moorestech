using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;
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
        public void AddLines(IReadOnlyList<TooltipLine> lines) => _lines.AddRange(lines);

        public void AddBlockedByTerrain() => _lines.Add(BlockedByTerrainLine());
        public void AddBlockedByExistingBlock() => _lines.Add(BlockedByExistingBlockLine());
        public void AddTooFar() => _lines.Add(TooFarLine());
        public void AddGroundNotFound() => _lines.Add(GroundNotFoundLine());

        // 共通不可理由の行を生成する。シンクを持たない純関数の判断側もここからキーを得る
        // Builds the shared block-reason lines so sink-less pure decision code takes its keys from here too
        public static TooltipLine BlockedByTerrainLine() => new(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain);
        public static TooltipLine BlockedByExistingBlockLine() => new(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock);
        public static TooltipLine TooFarLine() => new(LocalizationKeys.Ui.Tooltip.PlaceTooFar);
        public static TooltipLine GroundNotFoundLine() => new(LocalizationKeys.Ui.Tooltip.PlaceGroundNotFound);
    }
}
