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
            // 横（設定のスプリント）・縦で 1 つ無効（ビルドメニューの分類）・タブ（列車の時刻表）の 3 本
            // Horizontal (settings sprint), vertical with one disabled option (build menu categories), tablist (train timetable)
            var column = new VisualElement();
            var horizontal = new ModeSwitch<string>("gallery-mode-switch", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, Options("gallery-mode-switch"));
            horizontal.SetSelected("a");
            var vertical = new ModeSwitch<string>("gallery-mode-switch-vertical", ModeSwitchOrientation.Vertical, ModeSwitchRole.Group, Options("gallery-mode-switch-vertical"));
            vertical.SetSelected("b");
            vertical.SetOptionEnabled("c", false);
            var tablist = new ModeSwitch<string>("gallery-mode-switch-tablist", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Tablist, Options("gallery-mode-switch-tablist"));
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
