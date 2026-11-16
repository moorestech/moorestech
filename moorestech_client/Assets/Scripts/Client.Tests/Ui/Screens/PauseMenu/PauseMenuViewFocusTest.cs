using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Input;
using Client.Tests.Ui.Screens.Settings;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.BugReport;
using Client.Ui.Screens.PauseMenu;
using NUnit.Framework;
using UnityEngine.UIElements;
using Client.Ui.State.PauseMenu;

namespace Client.Tests.Ui.Screens.PauseMenu
{
    /// <summary>
    /// 入力欄にフォーカスがあるままポーズを隠しても入力中の扱いが残らないことを確かめる（L3 の入力排他）
    /// Verifies that hiding the pause menu while a text field is focused leaves no typing state behind (L3 input exclusivity)
    /// </summary>
    public class PauseMenuViewFocusTest : ScreenTestHost
    {
        [TearDown]
        public void ResetExclusivity()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
        }

        [Test]
        public void フォーカス中の入力欄を含むポーズを隠すと入力中の扱いが外れる()
        {
            // 基底はルートのコールバックを消さないので、テストごとの器に規則を付けて TearDown の Clear で捨てる
            // The base keeps root callbacks, so attach the rules to a per-test container discarded by the TearDown Clear
            var container = new VisualElement();
            rootVisualElement.Add(container);
            GameUiInputRules.Attach(container);
            var model = new FakePauseMenuModel(FakePauseMenuModel.Page(PauseMenuPage.BugReport));
            var settings = new FakeSettingsModel(FakeSettingsModel.Default());
            var view = new PauseMenuView(model.Current, settings.Current, new RecordingPauseMenuCommands(), new RecordingSettingsCommands(), Toasts, Toaster, new BugReportDraft(), new UiOsCursorRequestStack(new FakeUiOsCursorApplier()));
            container.Add(view);
            view.SetShown(true);
            simulate.FrameUpdate();
            Find<TextField>(view, "bug-report-description").Focus();
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused, "前提: 入力欄にフォーカスが入っている");

            // 隠すと UITK 自身がフォーカスを外し、外れが根を通って入力中の旗が下りる（画面は Blur しない。A-275）
            // Hiding makes UITK itself blur the field, and the blur passing the root lowers the text-input flag; the screen never blurs (A-275)
            view.SetShown(false);
            simulate.FrameUpdate();

            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }
    }
}
