using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Panels
{
    /// <summary>
    /// GamePanel の見本。持ち物パネル（default・題・整理ボタン）、題なしの default、中央詳細（craft）、題付きの craft を並べる
    /// GamePanel samples: the inventory panel (default, title, sort button), an untitled default, the center detail (craft) and a titled craft
    /// </summary>
    public sealed class GamePanelGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Panels;
        public string ComponentName => "GamePanel";

        public VisualElement CreateSample()
        {
            var row = PanelsAndDecorSampleSheet.CreateSampleRow();

            // 持ち物パネル（4-player-inventory）
            // Inventory panel (4-player-inventory)
            var inventory = new GamePanel("gallery-game-panel-inventory", GamePanelFrame.Default);
            inventory.SetTitle(new UiText(LocalizationKeys.Ui.Inventory.Title));
            inventory.SetTitleAction(new PanelActionButton("gallery-game-panel-sort", new UiText(LocalizationKeys.Ui.Inventory.Sort), new ProcessUiOsCursor(new UnityUiOsCursorApplier())));

            row.Add(PanelsAndDecorSampleSheet.Cell(inventory, "gallery-samples__inventory"));

            // 題なしの default
            // An untitled default
            var untitled = new GamePanel("gallery-game-panel-untitled", GamePanelFrame.Default);
            row.Add(PanelsAndDecorSampleSheet.Cell(untitled, "gallery-samples__craft-detail"));

            // 中央詳細と題付き craft
            // Center detail and titled craft
            var craft = new GamePanel("gallery-game-panel-craft", GamePanelFrame.Craft);
            row.Add(PanelsAndDecorSampleSheet.Cell(craft, "gallery-samples__craft-detail"));
            var titledCraft = new GamePanel("gallery-game-panel-craft-titled", GamePanelFrame.Craft);
            titledCraft.SetTitle(new UiText(LocalizationKeys.Ui.Inventory.Title));
            row.Add(PanelsAndDecorSampleSheet.Cell(titledCraft, "gallery-samples__craft-detail"));
            return row;
        }
    }
}
