using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Recipe
{
    // RecipeRowの見本（3 種）
    // RecipeRow samples (three kinds)
    public sealed class RecipeRowGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Recipe;
        public string ComponentName => "RecipeRow";

        public VisualElement CreateSample()
        {
            var root = RecipeGallerySampleSheet.CreateSampleRoot();
            AddSized(RecipeGallerySampleSheet.AddPlainRow(root), RecipeGallerySampleSheet.CreateCraftRow("gallery-recipe-craft", 2, 1, true));
            AddSized(RecipeGallerySampleSheet.AddPlainRow(root), RecipeGallerySampleSheet.CreateCraftRow("gallery-recipe-wrap", 3, 3, false));
            AddSized(RecipeGallerySampleSheet.AddRow(root, RecipeGallerySampleSheet.MachineSelectionKnobClass), RecipeGallerySampleSheet.CreateMachineSelectionOilRow("gallery-recipe-machine"));
            return root;

            #region Internal

            // 見本の幅はクラフトパネルの本文に近い 480px
            // Sample width is 480px, close to the craft panel body
            void AddSized(VisualElement parent, VisualElement row)
            {
                row.style.width = 480f;
                parent.Add(row);
            }

            #endregion
        }
    }
}
