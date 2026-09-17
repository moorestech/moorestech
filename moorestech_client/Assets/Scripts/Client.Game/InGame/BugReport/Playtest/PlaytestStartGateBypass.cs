using UnityEngine;

namespace Client.Game.InGame.BugReport.Playtest
{
    // 開始ゲートは応答があるまで初期化を止める。応答者のいない自動起動（テスト・プレイテストDSL）はここで迂回を宣言する
    // The start gates hold initialization until answered, so an unattended boot (tests, the playtest DSL) declares its bypass here
    // 印はEditorのSessionStateに置く。ドメインリロードは越えるがEditorプロセスと共に消え、ディスクに残らない（前例: PlaytestBootLifecycle）
    // The mark lives in the Editor's SessionState: it survives a domain reload but dies with the Editor process and never touches disk (precedent: PlaytestBootLifecycle)
    public static class PlaytestStartGateBypass
    {
        // 迂回する理由。印は読んだ時点で消費し、同じEditorでの次の手動再生にはゲートを戻す
        // Why the boot bypasses; the mark is consumed on read so the next manual play in the same Editor gets its gates back
        // 応答者が居ないまま待つと恒久停止するので、理由は必ず開発者ログへ出す側（ゲート）へ返す
        // Waiting with nobody to answer halts forever, so the reason is handed back to the gate that logs it
        public static string UnattendedReason()
        {
            var marked = ConsumeUnattendedBootMark();
            if (Application.isBatchMode) return "batchMode";
            return marked ? "unattendedBootMark" : null;
        }

#if UNITY_EDITOR
        private const string SessionStateKey = "PlaytestStartGateBypass_UnattendedBoot";

        // 自動起動の入口（テストのPlayMode突入・DSLの起動準備）から呼ぶ
        // Called from the unattended entry points (a test entering Play Mode, the DSL's boot preparation)
        public static void Apply()
        {
            UnityEditor.SessionState.SetBool(SessionStateKey, true);
        }

        private static bool ConsumeUnattendedBootMark()
        {
            var marked = UnityEditor.SessionState.GetBool(SessionStateKey, false);
            UnityEditor.SessionState.EraseBool(SessionStateKey);
            return marked;
        }
#else
        // 実機ビルドには自動起動の入口が無いので印も無い
        // A player build has no unattended entry point, so there is never a mark
        private static bool ConsumeUnattendedBootMark()
        {
            return false;
        }
#endif
    }
}
