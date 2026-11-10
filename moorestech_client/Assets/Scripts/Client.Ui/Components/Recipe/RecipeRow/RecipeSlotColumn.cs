using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Recipe
{
    /// <summary>
    /// レシピ行の素材／結果の列。スロットを 2 列で折り返し、列に収まらなければ開発者へ知らせる
    /// Material/result column of a recipe row; wraps slots in two columns and tells developers when they do not fit
    /// </summary>
    /// <remarks>
    /// Web の recipeSlotLayout は列数を 1〜2 にし、スロットの寸法を列の幅から縮める（cqw）。UITK はカスタムプロパティを C# から書けないので寸法は USS の上限のまま置き、はみ出しを LogError で知らせる。
    /// The Web recipeSlotLayout uses one or two columns and shrinks slots to the column width (cqw). UITK cannot set custom properties from C#, so slots keep the USS maximum and an overflow is reported through LogError.
    /// </remarks>
    internal sealed class RecipeSlotColumn : VisualElement
    {
        // Web の MAX_SLOT_COLUMNS（recipeSlotLayout.ts）
        // Web MAX_SLOT_COLUMNS (recipeSlotLayout.ts)
        private const int MaxSlotColumns = 2;

        // レイアウトの小数の丸めをはみ出しと数えない幅
        // Width that layout rounding may exceed without counting as overflow
        private const float OverflowTolerancePixels = 0.5f;

        private readonly string _rowTestid;
        private readonly VisualElement _grid;
        private bool _overflowReported;

        public RecipeSlotColumn(string rowTestid, string sideUssClassName)
        {
            _rowTestid = rowTestid;
            AddToClassList("recipe-row__slots");
            AddToClassList(sideUssClassName);
            pickingMode = PickingMode.Ignore;
            _grid = NewBox("recipe-row__slot-grid");
            Add(_grid);

            // 列と格子のどちらの寸法が変わっても収まりを見直す
            // Re-check the fit whenever either the column or the grid changes size
            RegisterCallback<GeometryChangedEvent>(_ => ReportOverflow());
            _grid.RegisterCallback<GeometryChangedEvent>(_ => ReportOverflow());
        }

        public void SetSlots(IReadOnlyList<VisualElement> slots)
        {
            // 列数は 1〜2。3 点以上は列を増やさず行を増やす（裁定 2026-08-20）
            // One or two columns; from three slots on, add rows rather than columns (ruling 2026-08-20)
            _grid.Clear();
            var columns = Math.Max(1, Math.Min(slots.Count, MaxSlotColumns));
            for (var start = 0; start < slots.Count; start += columns)
            {
                var line = NewBox("recipe-row__slot-line");
                line.EnableInClassList("recipe-row__slot-line--after", start > 0);
                _grid.Add(line);
                for (var i = start; i < Math.Min(start + columns, slots.Count); i++) line.Add(NewCell(slots[i], i > start));
            }

            #region Internal

            // 間隔は 2 点目以降の枠の左の margin で付ける（受けたスロットの要素には触らない）
            // Gaps come from the left margin of the cells after the first (the received slot elements stay untouched)
            VisualElement NewCell(VisualElement slot, bool afterFirst)
            {
                var cell = NewBox("recipe-row__slot-cell");
                cell.EnableInClassList("recipe-row__slot-cell--after", afterFirst);
                cell.Add(slot);
                return cell;
            }

            #endregion
        }

        private void ReportOverflow()
        {
            // 格子が列より広いと、Web なら縮むところがはみ出す。はみ出しに入った時だけ 1 回知らせる
            // A grid wider than the column overflows where the Web would shrink; report once when it starts overflowing
            var overflows = _grid.layout.width > contentRect.width + OverflowTolerancePixels;
            if (overflows && !_overflowReported) Debug.LogError($"[RecipeRow] {_rowTestid}: slots overflow the column ({_grid.layout.width:0.#}px > {contentRect.width:0.#}px); the Web shrinks them with container queries, UITK keeps --recipe-slot-size-max (look-exception: LX-91)");
            _overflowReported = overflows;
        }

        private static VisualElement NewBox(string ussClassName)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList(ussClassName);
            return box;
        }
    }
}
