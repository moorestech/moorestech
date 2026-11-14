using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Text;
using System.Collections.Generic;
using Client.Ui.Components.Controls;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    // ModeSwitchの見本（横・縦・タブの3本）
    // ModeSwitch sample (horizontal, vertical, tablist)
    public sealed class ModeSwitchGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "ModeSwitch";

        public VisualElement CreateSample()
        {
            // 横・縦（1 つ無効）・タブの 3 本
            // Horizontal, vertical (one disabled), and tablist
            var column = new VisualElement();
            var osCursor = new ProcessUiOsCursor(new UnityUiOsCursorApplier());
            var horizontal = new ModeSwitch<string>("gallery-mode-switch", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, Options("gallery-mode-switch"), osCursor);
            horizontal.SetSelected("a");
            var vertical = new ModeSwitch<string>("gallery-mode-switch-vertical", ModeSwitchOrientation.Vertical, ModeSwitchRole.Group, Options("gallery-mode-switch-vertical"), osCursor);
            vertical.SetSelected("b");
            vertical.SetOptionEnabled("c", false);
            var tablist = new ModeSwitch<string>("gallery-mode-switch-tablist", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Tablist, Options("gallery-mode-switch-tablist"), osCursor);

            tablist.SetSelected("a");
            column.Add(horizontal);
            column.Add(vertical);
            column.Add(tablist);
            return column;

            #region Internal

            IReadOnlyList<ModeSwitchOption<string>> Options(string prefix)
            {
                return new List<ModeSwitchOption<string>>
                {
                    new($"{prefix}-option-a", "a", UiText.FromVerbatim("Option A")),
                    new($"{prefix}-option-b", "b", UiText.FromVerbatim("Option B")),
                    new($"{prefix}-option-c", "c", UiText.FromVerbatim("Option C")),
                };
            }

            #endregion
        }
    }
}
