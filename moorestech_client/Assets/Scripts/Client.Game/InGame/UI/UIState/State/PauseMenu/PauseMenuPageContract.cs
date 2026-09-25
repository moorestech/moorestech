using System;

namespace Client.Game.InGame.UI.UIState.State.PauseMenu
{
    // 画面名の契約文字列。Webとの変換はここ1か所で行う（前例 PlaytestReportKindText）
    // Contract text for page names; conversion to and from the Web happens only here (precedent: PlaytestReportKindText)
    public static class PauseMenuPageContract
    {
        private const string TopText = "top";
        private const string SettingsText = "settings";
        private const string BugReportText = "bugReport";

        public static string ToContractText(PauseMenuPage page)
        {
            return page switch
            {
                PauseMenuPage.Top => TopText,
                PauseMenuPage.Settings => SettingsText,
                PauseMenuPage.BugReport => BugReportText,
                _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
            };
        }

        public static bool TryParse(string text, out PauseMenuPage page)
        {
            switch (text)
            {
                case TopText: page = PauseMenuPage.Top; return true;
                case SettingsText: page = PauseMenuPage.Settings; return true;
                case BugReportText: page = PauseMenuPage.BugReport; return true;
                default: page = PauseMenuPage.Top; return false;
            }
        }
    }
}
