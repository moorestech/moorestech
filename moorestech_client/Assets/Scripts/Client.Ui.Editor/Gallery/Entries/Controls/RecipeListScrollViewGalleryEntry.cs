using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Editor.Gallery.Entries.Recipe;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    // RecipeListScrollViewの見本（溢れるクラフト行・名前の段つきの機械のレシピ選択）
    // RecipeListScrollView samples (overflowing craft rows and the machine recipe selection with name bands)
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
            view.style.marginBottom = 16f;
            var rows = new List<VisualElement>();
            for (var i = 0; i < 5; i++) rows.Add(RecipeGallerySampleSheet.CreateCraftRow($"gallery-recipe-list-{i}", i % 3 + 1, 1, i % 2 == 0));
            view.SetEntries(rows);
            root.Add(view);

            // 機械のレシピ選択の上書きを当てた列。寸法は Web の参照の列（1280×720 で 702×317）で、行を多く積んでバーを出す
            // A column with the machine recipe selection overrides, sized like the Web reference column (702x317 at 1280x720), overfilled so the bar shows
            // ビューは flex-grow: 1 なので、寸法は縦並びの器に持たせる
            // The view has flex-grow: 1, so the size lives on a column host
            var machine = RecipeGallerySampleSheet.AddRow(root, RecipeGallerySampleSheet.MachineSelectionKnobClass);
            var column = new VisualElement();
            column.style.width = 702f;
            column.style.height = 317f;
            machine.Add(column);
            var machineView = new RecipeListScrollView("gallery-recipe-machine-list");
            var entries = new List<VisualElement>();
            for (var i = 0; i < 6; i++) entries.Add(RecipeGallerySampleSheet.CreateMachineSelectionEntry($"gallery-recipe-machine-list-{i}"));
            machineView.SetEntries(entries);
            column.Add(machineView);
            return root;
        }
    }
}
