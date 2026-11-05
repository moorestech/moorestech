using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.Ui.Screens.PauseMenu;
using Client.Tests.Ui.Screens.Settings;
using Client.Ui.Screens.BugReport;
using Client.Ui.Screens.PauseMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Client.Ui.State.PauseMenu;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// ポーズ内のバグ報告の本文欄が Web と同じ高さの複数行で、枠が最大高で止まっても縮まず本文がスクロールすることを確かめる（L1）
    /// Verifies the bug-report description inside the pause menu keeps the Web height as a multiline field and does not shrink when the frame stops at max height; the body scrolls (L1)
    /// </summary>
    public class BugReportDescriptionHeightTest : ScreenTestHost
    {
        private const float WebTextareaHeight = 72f;

        private PauseMenuView _view;

        [SetUp]
        public void MountBugReportPage()
        {
            var model = new FakePauseMenuModel(FakePauseMenuModel.Page(PauseMenuPage.BugReport));
            var settings = new FakeSettingsModel(FakeSettingsModel.Default());
            _view = Mount(new PauseMenuView(model.Current, settings.Current, new RecordingPauseMenuCommands(), new RecordingSettingsCommands(), Toasts, new BugReportDraft(), Catalog));
            _view.SetShown(true);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        [Test]
        public void 本文欄は複数行でトークンの高さを持ち中の入力欄まで伸びる()
        {
            var field = Find<TextField>(_view, "bug-report-description");
            Assert.IsTrue(field.multiline);
            Assert.AreEqual(WebTextareaHeight, field.parent.layout.height, 0.5f);
            Assert.AreEqual(WebTextareaHeight, field.layout.height, 0.5f);
            Assert.AreEqual(WebTextareaHeight, field.Q(className: "unity-base-text-field__input").layout.height, 0.5f);
        }

        [Test]
        public void 枠が最大高で止まっても本文欄は縮まず本文がスクロールする()
        {
            // ステージ高を 240px に縮めると枠の最大高は 160px になり、本文の中身より低くなる
            // Shrinking the stage to 240px makes the frame max height 160px, lower than the body content
            _view.style.bottom = StyleKeyword.Auto;
            _view.style.height = 240f;
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var body = Find<ScrollView>(_view, "pause-menu-body");
            var field = Find<TextField>(_view, "bug-report-description");
            Assert.Greater(body.contentContainer.layout.height, body.contentViewport.layout.height);
            Assert.AreEqual(WebTextareaHeight, field.parent.layout.height, 0.5f);
            Assert.AreEqual(WebTextareaHeight, field.Q(className: "unity-base-text-field__input").layout.height, 0.5f);
        }
    }
}
