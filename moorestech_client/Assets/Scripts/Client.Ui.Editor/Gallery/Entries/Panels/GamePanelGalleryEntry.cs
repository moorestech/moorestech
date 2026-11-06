using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
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

            // 参照 4-player-inventory の持ち物パネル
            // The inventory panel in reference 4-player-inventory
            var inventory = new GamePanel("gallery-game-panel-inventory", GamePanelFrame.Default);
            inventory.SetTitle(new UiText(LocalizationKeys.Ui.Inventory.Title));
            inventory.SetTitleAction(new PanelActionButton("gallery-game-panel-sort", LocalizationKeys.Ui.Inventory.Sort));
            row.Add(PanelsAndDecorSampleSheet.Cell(inventory, "gallery-samples__inventory"));

            // 題なしの default（題の行と罫線が出ないこと）
            // An untitled default (no title row or rules)
            var untitled = new GamePanel("gallery-game-panel-untitled", GamePanelFrame.Default);
            row.Add(PanelsAndDecorSampleSheet.Cell(untitled, "gallery-samples__craft-detail"));

            // 参照 4-player-inventory の中央詳細と、題付きの craft
            // The center detail in reference 4-player-inventory, and a titled craft
            var craft = new GamePanel("gallery-game-panel-craft", GamePanelFrame.Craft);
            row.Add(PanelsAndDecorSampleSheet.Cell(craft, "gallery-samples__craft-detail"));
            var titledCraft = new GamePanel("gallery-game-panel-craft-titled", GamePanelFrame.Craft);
            titledCraft.SetTitle(new UiText(LocalizationKeys.Ui.Inventory.Title));
            row.Add(PanelsAndDecorSampleSheet.Cell(titledCraft, "gallery-samples__craft-detail"));
            return row;
        }
    }
}
