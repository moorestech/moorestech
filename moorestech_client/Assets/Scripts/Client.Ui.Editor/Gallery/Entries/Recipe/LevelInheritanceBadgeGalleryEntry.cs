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
            var capped = new LevelInheritanceBadge("gallery-level-inheritance-capped");
            capped.Show(new UiText(LocalizationKeys.Ui.Recipe.LevelInheritanceCapped, new[] { new LocalizationTextParam("level", "2") }));
            RecipeGallerySampleSheet.AddRow(root, "recipe-gallery__plain").Add(capped);
            var plain = new LevelInheritanceBadge("gallery-level-inheritance");
            plain.Show(new UiText(LocalizationKeys.Ui.Recipe.LevelInheritance));
            RecipeGallerySampleSheet.AddRow(root, "recipe-gallery__plain").Add(plain);
            return root;
        }
    }
}
