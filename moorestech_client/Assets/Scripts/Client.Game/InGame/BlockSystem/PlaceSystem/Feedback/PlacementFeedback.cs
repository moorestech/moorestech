using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Tooltip;
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

        // 素材ごとの不足行の位置と内容。同一アイテムを1行に畳むために持つ
        // Where each material's shortage line sits and what it says, kept so the same item folds into one line
        private readonly Dictionary<ItemId, (int lineIndex, ConstructionMaterialShortage shortage)> _materialShortageLines = new();

        public IReadOnlyList<TooltipLine> Lines => _lines;

        public void Clear()
        {
            _lines.Clear();
            _materialShortageLines.Clear();
        }

        public void Add(TooltipLine line) => _lines.Add(line);

        /// <summary>
        /// 不足素材行を積む唯一の関門。建設コストと接続コストのように出所が複数あっても同一アイテムは1行に畳む
        /// The single gate for shortage lines; the same item folds into one line even when construction and connection costs push separately
        /// </summary>
        public void AddMaterialShortages(IReadOnlyList<ConstructionMaterialShortage> shortages)
        {
            foreach (var shortage in shortages) AddMaterialShortage(shortage);

            #region Internal

            // 既に同じアイテムの行があれば厳しい方へ書き換える。必要数は予約分を含む合計の方＝大きい方を採る（足すと二重計上になる）
            // Rewrites an existing line for the same item to the harsher one; the required count takes the larger value, which already includes the reservation (adding them would double count)
            // 所持数は同じ在庫から数えるので一致するはずだが、食い違ったら集めるべき量を過小に見せない小さい方を採る
            // The held count comes from the same inventory so it should match, but on a mismatch the smaller value wins so the amount left to gather is never understated
            void AddMaterialShortage(ConstructionMaterialShortage shortage)
            {
                if (!_materialShortageLines.TryGetValue(shortage.ItemId, out var existing))
                {
                    _materialShortageLines[shortage.ItemId] = (_lines.Count, shortage);
                    _lines.Add(ConstructionMaterialShortageLine.ToLine(shortage));
                    return;
                }

                var merged = new ConstructionMaterialShortage(
                    shortage.ItemId,
                    existing.shortage.Held < shortage.Held ? existing.shortage.Held : shortage.Held,
                    existing.shortage.Required < shortage.Required ? shortage.Required : existing.shortage.Required);

                _materialShortageLines[shortage.ItemId] = (existing.lineIndex, merged);
                _lines[existing.lineIndex] = ConstructionMaterialShortageLine.ToLine(merged);
            }

            #endregion
        }

        /// <summary>
        /// 不足素材行を積む。1件も無いとき（接続ツールのマスタ欠損など）は無言にせず汎用の不可文言1行へ落とす
        /// Pushes the shortage lines; with no shortage at all (e.g. a missing connect tool master) it falls back to one generic line instead of staying silent
        /// </summary>
        public void AddMaterialShortagesOrFallback(IReadOnlyList<ConstructionMaterialShortage> shortages, LocalizationKey emptyFallbackKey)
        {
            if (shortages.Count == 0)
            {
                _lines.Add(new TooltipLine(emptyFallbackKey));
                return;
            }

            AddMaterialShortages(shortages);
        }

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
