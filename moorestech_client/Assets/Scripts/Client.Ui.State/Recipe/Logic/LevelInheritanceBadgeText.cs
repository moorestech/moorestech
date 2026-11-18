using System;
using System.Globalization;
using Client.Localization;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Recipe.Logic
{
    /// <summary>
    /// 継承レシピのバッジの文（Web levelInheritanceBadge の写し）。継承しないレシピにはバッジを出さない
    /// Badge copy for an inheritance recipe (port of Web levelInheritanceBadge); no badge for a non-inheriting recipe
    /// </summary>
    public static class LevelInheritanceBadgeText
    {
        public static bool TryBuild(RecipeLevelInheritance inheritance, out UiText text)
        {
            switch (inheritance)
            {
                case NoLevelInheritance _:
                    text = null;
                    return false;
                case LevelInheritanceMapping { CappedAtUnlockedLevel: false }:
                    text = new UiText(LocalizationKeys.Ui.Recipe.LevelInheritance);
                    return true;
                case LevelInheritanceMapping capped:
                    // 上限つきは今の上限レベルを差し込む
                    // A capped recipe interpolates its current maximum level
                    var level = capped.MaxCraftableLevel.ToString(CultureInfo.InvariantCulture);
                    text = new UiText(LocalizationKeys.Ui.Recipe.LevelInheritanceCapped, new[] { new LocalizationTextParam("level", level) });
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(inheritance), inheritance.GetType().Name, "未知のレベル継承の型");
            }
        }
    }
}
