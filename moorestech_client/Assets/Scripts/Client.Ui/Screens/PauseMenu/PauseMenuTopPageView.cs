using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Ui.Components;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Assets;
using Client.Ui.Foundation.Text;
using Client.Ui.State.PauseMenu;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.PauseMenu
{
    /// <summary>
    /// ポーズのトップ。切断時の 1 行とセーブ 2・子画面の入口 2 のボタン（Web の PauseMenuTopPage・ADR 0069）
    /// Pause-menu top: the disconnected line plus two save actions and two sub-page entries (Web PauseMenuTopPage, ADR 0069)
    /// </summary>
    public sealed class PauseMenuTopPageView : VisualElement
    {
        private readonly LocalizedLabel _disconnected;

        public PauseMenuTopPageView(IPauseMenuCommands commands, UiAssetCatalog catalog)
        {
            // 切断の行は出ないときに間隔を残さないよう、ボタン列の外に置く
            // The disconnected line sits outside the button column so it leaves no gap when hidden
            AddToClassList("pause-menu__top-page");
            _disconnected = new LocalizedLabel { name = "pause-menu-disconnected" };
            _disconnected.AddToClassList("pause-menu__disconnected");
            _disconnected.SetKey(LocalizationKeys.Ui.PauseMenu.Disconnected);
            Add(_disconnected);
            var buttons = new VisualElement();
            Add(buttons);

            var save = AddButton("pause-menu-save", LocalizationKeys.Ui.Game.SaveGame, AnchorIds.PauseSave);
            save.clicked += commands.Save;
            var saveAndQuit = AddButton("pause-menu-save-and-quit", LocalizationKeys.Ui.Game.SaveAndQuit, AnchorIds.PauseBack);
            saveAndQuit.clicked += commands.SaveAndQuit;
            var settings = AddButton("pause-menu-open-settings", LocalizationKeys.Ui.PauseMenu.Settings, AnchorIds.PauseSettings);
            settings.clicked += () => commands.ShowPage(PauseMenuPage.Settings);
            var bugReport = AddButton("pause-menu-open-bug-report", LocalizationKeys.Ui.PauseMenu.BugReport, AnchorIds.PauseBugReport);
            bugReport.clicked += () => commands.ShowPage(PauseMenuPage.BugReport);

            #region Internal

            PanelActionButton AddButton(string testid, LocalizationKey label, string anchorId)
            {
                var button = new PanelActionButton(testid, label, catalog.PanelActionButton);
                TutorialAnchor.Mark(button, anchorId);
                PauseMenuStack.Add(buttons, button);
                return button;
            }

            #endregion
        }

        public void SetDisconnected(bool disconnected)
        {
            _disconnected.style.display = disconnected ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
