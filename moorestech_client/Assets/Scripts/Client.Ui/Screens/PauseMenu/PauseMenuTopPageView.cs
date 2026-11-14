using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズのトップ。切断時の 1 行とセーブ 2・子画面の入口 2 のボタン（Web の PauseMenuTopPage・ADR 0069）
    /// Pause-menu top: the disconnected line plus two save actions and two sub-page entries (Web PauseMenuTopPage, ADR 0069)
    /// </summary>
    public sealed class PauseMenuTopPageView : VisualElement, IPauseMenuPage
    {
        private readonly LocalizedLabel _disconnected;

        public PauseMenuTopPageView(IPauseMenuCommands commands)
        {
            // 切断の行は出ないときに間隔を残さないよう、ボタン列の外に置く
            // The disconnected line sits outside the button column so it leaves no gap when hidden
            AddToClassList("pause-menu__top-page");
            _disconnected = new LocalizedLabel { name = "pause-menu-disconnected" };
            _disconnected.AddToClassList("pause-menu__disconnected");
            _disconnected.SetKey(LocalizationKeys.Ui.PauseMenu.Disconnected);
            Add(_disconnected);
            var buttons = new UiStack(UiGap.Md);
            Add(buttons);

            var save = AddButton("pause-menu-save", LocalizationKeys.Ui.Game.SaveGame, AnchorIds.PauseSave);
            save.OnClicked.Subscribe(_ => commands.Save());
            var saveAndQuit = AddButton("pause-menu-save-and-quit", LocalizationKeys.Ui.Game.SaveAndQuit, AnchorIds.PauseBack);
            saveAndQuit.OnClicked.Subscribe(_ => commands.SaveAndQuit());
            var settings = AddButton("pause-menu-open-settings", LocalizationKeys.Ui.PauseMenu.Settings, AnchorIds.PauseSettings);
            settings.OnClicked.Subscribe(_ => commands.ShowPage(PauseMenuPage.Settings));
            var bugReport = AddButton("pause-menu-open-bug-report", LocalizationKeys.Ui.PauseMenu.BugReport, AnchorIds.PauseBugReport);
            bugReport.OnClicked.Subscribe(_ => commands.ShowPage(PauseMenuPage.BugReport));

            #region Internal

            PanelActionButton AddButton(string testid, LocalizationKey label, string anchorId)
            {
                var button = new PanelActionButton(testid, new UiText(label));
                TutorialAnchor.Mark(button, anchorId);
                buttons.AddItem(button);
                return button;
            }

            #endregion
        }

        public VisualElement Root => this;

        public void ApplyPauseMenu(PauseMenuViewModel model)
        {
            UiDisplay.SetShown(_disconnected, model.Disconnected);
        }

        public void ApplySettings(SettingsViewModel model)
        {
        }
    }
}
