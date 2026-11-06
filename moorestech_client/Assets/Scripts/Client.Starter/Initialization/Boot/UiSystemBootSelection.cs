using Client.Input;
using UnityEngine;

namespace Client.Starter.Initialization.Boot
{
    /// <summary>
    /// 描く系（Web か GameUi）を起動で1回だけ決め、入力の裁きへ押し込む（plan §3.3 R1・R4）
    /// Decides the drawing system (Web or GameUi) once per boot and pushes it into input arbitration (plan §3.3 R1, R4)
    /// </summary>
    internal static class UiSystemBootSelection
    {
        internal static UiSystem ResolveAndApply()
        {
#if UNITY_EDITOR
            var system = UiSystemEditorSwitch.Current;
#else
            // 配布ビルドにスイッチは無く、いつも Web で描く（R4）
            // Player builds have no switch and always draw with Web (R4)
            var system = UiSystem.Web;
#endif
            // どちらの系で起動したかは毎回ログに残す（Web の起動で Client.Ui を登録しない理由の手掛かり）
            // Always log which system this boot uses (the clue for why a Web boot registers nothing from Client.Ui)
            Debug.Log($"[UiSystem] this boot draws with {system}");
            UiInputExclusivity.SetActiveUiSystem(system);
            return system;
        }
    }
#if UNITY_EDITOR
    /// <summary>
    /// Editor 限定の全体スイッチ。次の起動から全部 Web か全部 GameUi(UITK) で描く。既定は Web（plan §3.3 R4）
    /// Editor-only global switch; from the next boot everything draws with Web or with GameUi (UITK); the default is Web (plan §3.3 R4)
    /// 値は SessionState に置くので PlayMode 入りのドメインリロードを越え、Editor を閉じると既定へ戻る。切替 PR（5a）でスイッチごと消す
    /// The value lives in SessionState, so it survives the play-mode domain reload and returns to the default when the Editor closes; the cutover PR (5a) deletes it
    /// </summary>
    public static class UiSystemEditorSwitch
    {
        private const string SessionStateKey = "UiSystemEditorSwitch_GameUi";
        private const string WebMenuPath = "moorestech/UI System/Web";
        private const string GameUiMenuPath = "moorestech/UI System/GameUi (UITK)";

        public static UiSystem Current => UnityEditor.SessionState.GetBool(SessionStateKey, false) ? UiSystem.GameUi : UiSystem.Web;

        // PlayMode 中に変えても走っている起動には効かない。起動ごとに1回だけ読むため
        // Changing it during play mode does not affect the running boot, because it is read once per boot
        public static void Set(UiSystem system)
        {
            UnityEditor.SessionState.SetBool(SessionStateKey, system == UiSystem.GameUi);
            Debug.Log($"[UiSystemEditorSwitch] the next boot draws with {system}");
        }

        [UnityEditor.MenuItem(WebMenuPath)]
        private static void SelectWeb()
        {
            Set(UiSystem.Web);
        }

        [UnityEditor.MenuItem(GameUiMenuPath)]
        private static void SelectGameUi()
        {
            Set(UiSystem.GameUi);
        }

        // メニューの印を今の値に合わせる
        // Keep the menu check marks in step with the current value
        [UnityEditor.MenuItem(WebMenuPath, true)]
        private static bool ValidateWeb()
        {
            UnityEditor.Menu.SetChecked(WebMenuPath, Current == UiSystem.Web);
            return true;
        }

        [UnityEditor.MenuItem(GameUiMenuPath, true)]
        private static bool ValidateGameUi()
        {
            UnityEditor.Menu.SetChecked(GameUiMenuPath, Current == UiSystem.GameUi);
            return true;
        }
    }
#endif
}
