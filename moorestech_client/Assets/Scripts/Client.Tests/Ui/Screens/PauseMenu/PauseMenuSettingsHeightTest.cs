using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Localization;
using Client.Tests.Ui.Screens.Settings;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.PauseMenu;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.PauseMenu
{
    /// <summary>
    /// 設定画面の縦寸法を Web の CSS 値で固定する
    /// Pins the settings page vertical sizes to Web CSS values
    /// </summary>
    public class PauseMenuSettingsHeightTest : ScreenTestHost
    {
        private const float TitleOrder1Height = 44.2f;
        private const float TitleOrder2Height = 35.1f;
        private const float TextHeight = 24.8f;
        private const float OptionHeight = 34.8f;
        private const float StackGap = 16f;
        private const float OptionGap = 4f;
        private const float PixelSnapTolerance = 1f;

        // 言語: 題＋縦の選択肢 4 つ（間 4px）。走り: 題＋横の選択肢。画質: 題＋（項目名＋横の選択肢）× 10
        // Language: title + 4 vertical options (4px apart); Sprint: title + a horizontal row; Graphics: title + (caption + row) x 10
        private const float LanguageSectionHeight = TitleOrder2Height + OptionHeight * 4 + OptionGap * 3;
        private const float SprintSectionHeight = TitleOrder2Height + OptionHeight;
        private const float GraphicsSectionHeight = TitleOrder2Height + (TextHeight + OptionHeight) * 10;

        private PauseMenuView _view;

        [TestCase("english")]
        [TestCase("japanese")]
        public void 設定画面の題と各節の縦の寸法がWebのCSSの値と1px以内で揃う(string languageCode)
        {
            MountSettings(languageCode);

            // 題の行は Title order=1 の 1 行ぶん、本文は題の下 gap=md から始まる
            // The title row is one Title order=1 line and the body starts gap=md below it
            var frame = Find<VisualElement>(_view, "pause-menu").worldBound;
            var title = _view.Q<VisualElement>(className: "pause-menu__title").worldBound;
            var body = Find<GameScrollView>(_view, "pause-menu-body");
            AssertNear(TitleOrder1Height, title.height, "title height");
            AssertNear(frame.y + 1f + 32f + TitleOrder1Height + StackGap, body.worldBound.y, "body top");

            // 各節の見出しの y と本文全体の高さ
            // Each section heading's y and the whole body height
            var headings = body.Query<VisualElement>(className: "settings-section__title").ToList();
            Assert.AreEqual(3, headings.Count);
            var bodyTop = body.contentContainer.worldBound.y;
            AssertNear(0f, headings[0].worldBound.y - bodyTop, "language heading");
            AssertNear(LanguageSectionHeight + StackGap, headings[1].worldBound.y - bodyTop, "sprint heading");
            AssertNear(LanguageSectionHeight + SprintSectionHeight + StackGap * 2, headings[2].worldBound.y - bodyTop, "graphics heading");
            AssertNear(LanguageSectionHeight + SprintSectionHeight + GraphicsSectionHeight + StackGap * 2, body.contentContainer.layout.height, "body content height");
        }

        private void MountSettings(string languageCode)
        {
            // 言語を先に決めてから載せ、字形（CJK/Latin）を最初のレイアウトに入れる
            // Pick the language before mounting so the first layout uses its glyph face (CJK/Latin)
            Localize.TrySetChosenLanguage(languageCode);
            var model = new FakePauseMenuModel(FakePauseMenuModel.Page(PauseMenuPage.Settings));
            var settings = new FakeSettingsModel(FakeSettingsModel.Default());
            _view = Mount(new PauseMenuView(model.Current, settings.Current, new RecordingPauseMenuCommands(), new RecordingSettingsCommands(), Toasts, Toaster, new BugReportDraft()));
            _view.SetShown(true);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        private static void AssertNear(float expected, float actual, string what)
        {
            Assert.AreEqual(expected, actual, PixelSnapTolerance, what);
        }
    }
}
