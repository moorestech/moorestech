using Client.Ui.Editor.Gallery.Entries.Recipe;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    // RecipeActionButtonの見本（5 状態）
    // RecipeActionButton samples (five states)
    public sealed class RecipeActionButtonGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "RecipeActionButton";

        public VisualElement CreateSample()
        {
            var root = RecipeGallerySampleSheet.CreateSampleRoot();
            var craft = RecipeGallerySampleSheet.AddPlainRow(root);
            craft.Add(RecipeGallerySampleSheet.CreateCraftButton("gallery-recipe-action-craft", true));
            craft.Add(RecipeGallerySampleSheet.CreateCraftButton("gallery-recipe-action-craft-disabled", false));
            craft.Add(RecipeGalleryHoverDriver.HoverWhenLaidOut(RecipeGallerySampleSheet.CreateCraftButton("gallery-recipe-action-craft-hover", true)));
            var research = RecipeGallerySampleSheet.AddRow(root, RecipeGallerySampleSheet.ResearchKnobClass);
            research.Add(RecipeGallerySampleSheet.CreateResearchButton("gallery-recipe-action-research", true));
            research.Add(RecipeGallerySampleSheet.CreateResearchButton("gallery-recipe-action-research-disabled", false));
            return root;
        }
    }
}
