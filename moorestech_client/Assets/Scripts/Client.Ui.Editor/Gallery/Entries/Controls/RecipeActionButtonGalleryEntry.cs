using Client.Ui.Editor.Gallery.Entries.Recipe;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    // RecipeActionButtonの見本（クラフト・研究 × 有効・無効。ホバーと押下はギャラリー上で手で確かめる）
    // RecipeActionButton samples (craft and research, enabled and disabled; hover and press are checked by hand in the gallery)
    public sealed class RecipeActionButtonGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "RecipeActionButton";

        public VisualElement CreateSample()
        {
            var root = RecipeGallerySampleSheet.CreateSampleRoot();
            var craft = RecipeGallerySampleSheet.AddRow(root, "recipe-gallery__plain");
            craft.Add(RecipeGallerySampleSheet.CreateCraftButton("gallery-recipe-action-craft", true));
            craft.Add(RecipeGallerySampleSheet.CreateCraftButton("gallery-recipe-action-craft-disabled", false));
            var research = RecipeGallerySampleSheet.AddRow(root, RecipeGallerySampleSheet.ResearchKnobClass);
            research.Add(RecipeGallerySampleSheet.CreateResearchButton("gallery-recipe-action-research", true));
            research.Add(RecipeGallerySampleSheet.CreateResearchButton("gallery-recipe-action-research-disabled", false));
            return root;
        }
    }
}
