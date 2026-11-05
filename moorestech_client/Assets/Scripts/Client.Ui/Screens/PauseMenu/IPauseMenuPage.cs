using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズの 1 ページ。モデル変化の受け口を全ページが持ち、足し忘れを型で防ぐ
    /// One pause-menu page; every page owns both model entry points so a forgotten Apply cannot compile
    /// </summary>
    public interface IPauseMenuPage
    {
        VisualElement Root { get; }
        void ApplyPauseMenu(PauseMenuViewModel model);
        void ApplySettings(SettingsViewModel model);
    }
}
