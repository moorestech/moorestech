using Client.Input;
using Client.Starter.Initialization.Boot;
using NUnit.Framework;

namespace Client.Tests.Ui.Routing
{
    // 全体スイッチの値が次の起動の解決に乗り、入力の裁きへ押し込まれる（plan §3.3 R1・R4）
    // The global switch's value reaches the next boot's resolution and is pushed into input arbitration (plan §3.3 R1, R4)
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
