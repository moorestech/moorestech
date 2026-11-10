using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Editor.Gallery.Entries.Recipe;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    // RecipeListScrollViewの見本（溢れる 5 行）
    // RecipeListScrollView sample (five overflowing rows)
    public sealed class RecipeListScrollViewGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "RecipeListScrollView";

        public VisualElement CreateSample()
        {
            var root = RecipeGallerySampleSheet.CreateSampleRoot();
            var view = new RecipeListScrollView("gallery-recipe-list");
            view.style.width = 500f;
            view.style.height = 260f;
            var rows = new List<VisualElement>();
            for (var i = 0; i < 5; i++) rows.Add(RecipeGallerySampleSheet.CreateCraftRow($"gallery-recipe-list-{i}", i % 3 + 1, 1, i % 2 == 0));
            view.SetEntries(rows);
            root.Add(view);
            return root;
        }
    }
}
