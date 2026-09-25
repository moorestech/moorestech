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

        // この起動が無人かは初回の解決で1度だけ決まる。印を消費した後に読む側（退避・常時記録）が別の答えを得ないようラッチする
        // Whether this boot is unattended is settled once at the first resolution; it is latched so readers after the mark is consumed (salvage, capture) never get another answer
        private static bool _unattendedReasonResolved;
        private static string _resolvedUnattendedReason;

        // タイトル以外のシーンから始まった起動の宣言理由。ゲートを出さない理由の置き場をこのクラスへ揃える
        // The declared reason for a boot that started outside the title; every reason for not showing the gates lives in this class
        private static string _directBootReason;

        // Editorの再生し直しは同じプロセスで起動をやり直すため、再生ごとに未解決へ戻す（前例: PlaytestLaunchProfile）
        // An Editor replay restarts the boot in the same process, so each play returns to unresolved (precedent: PlaytestLaunchProfile)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnPlayMode()
        {
            _unattendedReasonResolved = false;
            _resolvedUnattendedReason = null;
            _directBootReason = null;
        }

        // 迂回する理由。Editorの印は初回の解決で消費し、同じEditorでの次の手動再生にはゲートを戻す
        // Why the boot bypasses; the Editor mark is consumed at the first resolution so the next manual play in the same Editor gets its gates back
        // 応答者が居ないまま待つと恒久停止するので、理由は必ず開発者ログへ出す側（ゲート）へ返す
        // Waiting with nobody to answer halts forever, so the reason is handed back to the gate that logs it
        public static string UnattendedReason()
        {
            if (_unattendedReasonResolved) return _resolvedUnattendedReason;

            _resolvedUnattendedReason = ResolveUnattendedReason();
            _unattendedReasonResolved = true;
            EraseUnattendedBootMark();
            return _resolvedUnattendedReason;
        }

        // 印を消費せずに同じ判定を返す。開始ゲートより先に走る判定（直Playの常時記録）が使う。解決済みならラッチした答えを返す
        // Returns the same decision without consuming the mark, for decisions that run before the start gates (direct-play capture); once resolved it returns the latched answer
        public static string PeekUnattendedReason()
        {
            if (_unattendedReasonResolved) return _resolvedUnattendedReason;
            return ResolveUnattendedReason();
        }

        // タイトルを通らない起動の入口から呼ぶ。通過の理由は必ず残し、配布版で確認を飛ばしている経路を無音にしない
        // Called from the entry of a boot that skips the title; the reason is always logged so a path skipping the confirmations in a distribution build is never silent
        public static void DeclareDirectBoot(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                Debug.LogError("PlaytestStartGateBypass: 理由の無い直接起動の宣言は受け付けません（タイトルのゲートは迂回されません）");
                return;
            }
            _directBootReason = reason;
            Debug.Log($"PlaytestStartGateBypass: タイトルのゲートを出さずに通します reason:{reason}（未応答の印は残り、次にタイトルを通る起動で聞き直します）");
        }

        // 直接起動と宣言された理由。宣言が無ければnull
        // The declared direct-boot reason, or null when none was declared
        public static string DirectBootReason()
        {
            return _directBootReason;
        }

        private static string ResolveUnattendedReason()
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

        // リロード後も終了を購読し、未消費の印を次のPlayへ漏らさない
        // Resubscribe after reload so an unconsumed mark cannot leak into the next play
        [UnityEditor.InitializeOnLoadMethod]
        private static void Initialize()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            // 再生中にラッチした答えは編集モードへ持ち越さない（終了では静的状態がリロードされない）
            // The answer latched during play is not carried into edit mode (exiting play does not reload statics)
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                EraseUnattendedBootMark();
                ResetOnPlayMode();
            }
        }

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
