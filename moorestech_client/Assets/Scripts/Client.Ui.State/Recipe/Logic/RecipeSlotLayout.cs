using System;

namespace Client.Ui.State.Recipe.Logic
{
    /// <summary>
    /// レシピ行のスロットの列数と 1 枠の寸法
    /// Column count and per-slot size for a recipe row
    /// </summary>
    public readonly struct RecipeSlotLayoutResult
    {
        public readonly int Columns;
        public readonly float SlotSize;

        public RecipeSlotLayoutResult(int columns, float slotSize)
        {
            Columns = columns;
            SlotSize = slotSize;
        }
    }

    /// <summary>
    /// 点数から列数と、列の幅に収まるスロット寸法を引く（Web recipeSlotLayout の写し。Web の cqw は列の実幅を受けて計算する）
    /// Derives the column count and the slot size fitting the column width (port of Web recipeSlotLayout; cqw becomes the measured column width)
    /// </summary>
    public static class RecipeSlotLayout
    {
        // 折り返し後の列数は 2 で固定し、点数が増えたぶんは行が増える（ユーザー裁定 2026-08-20。列を増やすと個数の文字が読めなくなる）
        // Wrapping keeps two columns and grows rows (user ruling 2026-08-20; more columns shrink slots until counts are unreadable)
        public const int MaxSlotColumns = 2;

        public static RecipeSlotLayoutResult Compute(int count, float columnWidth, float slotSizeMax, float slotGap)
        {
            // 0 点でも 1 列を床にし、0 列・0 除算を出さない
            // Floor at one column so zero items never yield zero columns or a divide-by-zero
            var columns = Math.Max(1, Math.Min(count, MaxSlotColumns));

            // 列間の gap を (列数-1) 本引いて等分し、上限で頭打ちにする
            // Subtract (columns-1) gaps, split evenly and cap at the maximum
            var fitted = (columnWidth - (columns - 1) * slotGap) / columns;
            return new RecipeSlotLayoutResult(columns, Math.Min(slotSizeMax, fitted));
        }
    }
}
