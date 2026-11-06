using Client.Input;
using Client.Starter.Initialization.Boot;
using NUnit.Framework;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// 全体スイッチを選んで PlayMode に入る L3 の土台。テストの後は成否によらず既定の Web へ戻す（plan §3.3 R4）
    /// Base for L3 tests that pick the global switch before entering play mode; afterwards it restores the default Web whatever the outcome (plan §3.3 R4)
    /// </summary>
    public abstract class UiSystemPlayModeTestBase
    {
        // EnterPlayMode より前に呼ぶ。値は SessionState にありドメインリロードを越えて次の起動に効く
        // Call before EnterPlayMode; the value lives in SessionState and reaches the next boot across the domain reload
        protected static void SelectUiSystemBeforePlayMode(UiSystem system)
        {
            UiSystemEditorSwitch.Set(system);
        }

        [TearDown]
        public void RestoreDefaultUiSystem()
        {
            UiSystemEditorSwitch.Set(UiSystem.Web);
        }
    }
}
