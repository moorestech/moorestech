using Client.Input;
using Client.Starter.Initialization.Boot;
using NUnit.Framework;

namespace Client.Tests.Ui.Routing
{
    // スイッチ値が起動と入力へ届く
    // The switch value reaches boot and input
    public class UiSystemSwitchTest
    {
        [TearDown]
        public void TearDown()
        {
            UiSystemEditorSwitch.Set(UiSystem.Web);
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
        }

        [Test]
        public void GameUiへ切り替えた起動はGameUiで描き入力の裁きもGameUiになる()
        {
            UiSystemEditorSwitch.Set(UiSystem.GameUi);

            Assert.AreEqual(UiSystem.GameUi, UiSystemEditorSwitch.Current);
            Assert.AreEqual(UiSystem.GameUi, UiSystemBootSelection.ResolveAndApply());
            Assert.AreEqual(UiSystem.GameUi, UiInputExclusivity.ActiveUiSystem);
        }

        [Test]
        public void Webへ戻した起動はWebで描く()
        {
            UiSystemEditorSwitch.Set(UiSystem.GameUi);
            UiSystemEditorSwitch.Set(UiSystem.Web);

            Assert.AreEqual(UiSystem.Web, UiSystemEditorSwitch.Current);
            Assert.AreEqual(UiSystem.Web, UiSystemBootSelection.ResolveAndApply());
            Assert.AreEqual(UiSystem.Web, UiInputExclusivity.ActiveUiSystem);
        }
    }
}
