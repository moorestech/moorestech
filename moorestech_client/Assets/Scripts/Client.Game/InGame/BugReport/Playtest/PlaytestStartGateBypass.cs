using UnityEngine;

namespace Client.Game.InGame.BugReport.Playtest
{
    // 開始ゲートは応答があるまで初期化を止める。応答者のいない自動起動（テスト・プレイテストDSL）はここで迂回を宣言する
    // The start gates hold initialization until answered, so an unattended boot (tests, the playtest DSL) declares its bypass here
    // 印はEditorのSessionStateに置く。ドメインリロードは越えるがEditorプロセスと共に消え、ディスクに残らない（前例: PlaytestBootLifecycle）
    // The mark lives in the Editor's SessionState: it survives a domain reload but dies with the Editor process and never touches disk (precedent: PlaytestBootLifecycle)
    public static class PlaytestStartGateBypass
    {
        // 実機プロセス全体が無人と宣言された理由（配布ビルドのsmoke等）。プロセスごと使い捨てなので消費しない
        // Why the whole player process was declared unattended (e.g. the distribution smoke run); the process is disposable, so it is never consumed
        private static string _unattendedProcessReason;

        // 迂回する理由。印は読んだ時点で消費し、同じEditorでの次の手動再生にはゲートを戻す
        // Why the boot bypasses; the mark is consumed on read so the next manual play in the same Editor gets its gates back
        // 応答者が居ないまま待つと恒久停止するので、理由は必ず開発者ログへ出す側（ゲート）へ返す
        // Waiting with nobody to answer halts forever, so the reason is handed back to the gate that logs it
        public static string UnattendedReason()
        {
            var reason = PeekUnattendedReason();
            EraseUnattendedBootMark();
            return reason;
        }

        // 印を消費せずに同じ判定を返す。開始ゲートより先に走る判定（直Playの常時記録）が使う
        // Returns the same decision without consuming the mark, for decisions that run before the start gates (direct-play capture)
        public static string PeekUnattendedReason()
        {
            if (Application.isBatchMode) return "batchMode";
            if (_unattendedProcessReason != null) return _unattendedProcessReason;
            return IsUnattendedBootMarked() ? "unattendedBootMark" : null;
        }

        // 引数で自動運転される実機プロセスの入口から呼ぶ。退避物は迂回しても last-session に残り、通常のsalvageとして扱われる
        // Called from the entry point of a player process driven by arguments; the salvage stays in last-session and is handled as usual
        public static void DeclareUnattendedProcess(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                Debug.LogError("PlaytestStartGateBypass: 理由の無い無人宣言は受け付けません（開始ゲートは迂回されません）");
                return;
            }
            _unattendedProcessReason = reason;
        }

#if UNITY_EDITOR
        private const string SessionStateKey = "PlaytestStartGateBypass_UnattendedBoot";

        // 自動起動の入口（テストのPlayMode突入・DSLの起動準備）から呼ぶ
        // Called from the unattended entry points (a test entering Play Mode, the DSL's boot preparation)
        public static void Apply()
        {
            UnityEditor.SessionState.SetBool(SessionStateKey, true);
        }

        private static bool IsUnattendedBootMarked()
        {
            return UnityEditor.SessionState.GetBool(SessionStateKey, false);
        }

        private static void EraseUnattendedBootMark()
        {
            UnityEditor.SessionState.EraseBool(SessionStateKey);
        }
#else
        // 実機ビルドにはEditorの印が無い。実機の無人起動は DeclareUnattendedProcess で宣言する
        // A player build has no Editor mark; a player's unattended boot is declared through DeclareUnattendedProcess
        private static bool IsUnattendedBootMarked()
        {
            return false;
        }

        private static void EraseUnattendedBootMark()
        {
        }
#endif
    }
}
