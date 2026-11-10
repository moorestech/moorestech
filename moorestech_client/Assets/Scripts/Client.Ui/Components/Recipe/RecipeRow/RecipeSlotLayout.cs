using System;

namespace Client.Ui.Components.Recipe
{
    /// <summary>
    /// レシピ行のスロットの列数の唯一の式（Web recipeSlotLayout の列数の写し）
    /// The single column-count formula for recipe-row slots (port of the Web recipeSlotLayout column count)
    /// </summary>
    /// <remarks>
    /// Web は列の幅からスロットの寸法も縮めるが、UITK は寸法をつまみの上限のまま置く（A-121・LX-91）ので列数だけを写す。
    /// The Web also shrinks slots to the column width, but UITK keeps the knob maximum (A-121, LX-91), so only the column count is ported.
    /// </remarks>
    internal static class RecipeSlotLayout
    {
        // 折り返し後の列数は 2 で固定し、点数が増えたぶんは行が増える（ユーザー裁定 2026-08-20。列を増やすと個数の文字が読めなくなる。Web MAX_SLOT_COLUMNS）
        // Wrapping keeps two columns and grows rows (user ruling 2026-08-20; more columns make counts unreadable; Web MAX_SLOT_COLUMNS)
        public const int MaxSlotColumns = 2;

        public static int Columns(int count)
        {
            // 0 点でも 1 列を床にし、0 列を出さない
            // Floor at one column so zero items never yield zero columns
            return Math.Max(1, Math.Min(count, MaxSlotColumns));
        }
    }
}
