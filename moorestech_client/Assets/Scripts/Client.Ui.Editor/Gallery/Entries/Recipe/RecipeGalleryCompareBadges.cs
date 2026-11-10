using Client.Localization;
using Client.Ui.Components.Recipe;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Recipe
{
    /// <summary>
    /// 並べ比べの継承バッジ 2 種（継承・上限つき継承）。Web の機械のレシピ選択のバッジと同じ幅で並べ、文の段組を比べる
    /// Comparison inheritance badges (inherit and capped); laid out at the Web machine-selection badge width to compare the text layout
    /// </summary>
    public static class RecipeGalleryCompareBadges
    {
        public static VisualElement Create(float width, string cappedLevel)
        {
            var root = RecipeGallerySampleSheet.CreateSampleRoot();
            root.Add(CreateBadge("compare-badge-inherit", width, new UiText(LocalizationKeys.Ui.Recipe.LevelInheritance)));
            root.Add(CreateBadge("compare-badge-capped", width, new UiText(LocalizationKeys.Ui.Recipe.LevelInheritanceCapped, new[] { new LocalizationTextParam("level", cappedLevel) })));
            return root;
        }

        // Web の spec の幅で置き、撮影の切り抜きが重ならないよう下に間をあける
        // Place at the Web spec width with a gap below so the crops never overlap
        private static LevelInheritanceBadge CreateBadge(string testid, float width, UiText text)
        {
            var badge = new LevelInheritanceBadge(testid);
            badge.style.width = width;
            badge.style.marginBottom = 16f;
            badge.Show(text);
            return badge;
        }
    }
}
