using Client.Localization;
using Client.Ui.Components.Recipe;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Recipe
{
    // LevelInheritanceBadgeの見本（上限つき・上限なし）
    // LevelInheritanceBadge samples (capped and uncapped)
    public sealed class LevelInheritanceBadgeGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Recipe;
        public string ComponentName => "LevelInheritanceBadge";

        public VisualElement CreateSample()
        {
            var root = RecipeGallerySampleSheet.CreateSampleRoot();
            AddSized(new LevelInheritanceBadge("gallery-level-inheritance-capped"), new UiText(LocalizationKeys.Ui.Recipe.LevelInheritanceCapped, new[] { new LocalizationTextParam("level", "2") }));
            AddSized(new LevelInheritanceBadge("gallery-level-inheritance"), new UiText(LocalizationKeys.Ui.Recipe.LevelInheritance));
            return root;

            #region Internal

            // 幅は Web の機械のレシピ選択の項目と同じ 688px（1280×720 の実測）
            // Width matches the Web machine recipe selection entry, 688px (measured at 1280x720)
            void AddSized(LevelInheritanceBadge badge, UiText text)
            {
                badge.style.width = 688f;
                badge.Show(text);
                RecipeGallerySampleSheet.AddPlainRow(root).Add(badge);
            }

            #endregion
        }
    }
}
