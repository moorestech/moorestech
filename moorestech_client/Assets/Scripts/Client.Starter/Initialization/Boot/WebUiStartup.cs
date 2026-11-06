using System;
using System.Threading;
using Client.Game.InGame.BugReport.WebUiHealth;
using Client.Input;
using Client.WebUiHost.Health;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Initialization.Boot
{
    internal static class WebUiStartup
    {
        internal static async UniTask<bool> StartAsync(CancellationToken exitToken, UiSystem uiSystem)
        {
#if UNITY_EDITOR
            // 全体スイッチが GameUi の起動と印のある起動は、起動失敗と同経路で Web ホストを起動しない（plan §2.1 0-10・§3.3 R1）。印は毎回消費して次の起動へ漏らさない
            // A GameUi boot and a marked boot skip the web host along the startup-failure path (plan §2.1 0-10, §3.3 R1); the mark is always consumed so it never leaks into the next boot
            var skipMarked = WebUiHostEditorSkip.ConsumeSkipMark();
            var skipHost = uiSystem == UiSystem.GameUi || skipMarked;
            // 飛ばしたかは WebUiHost の起動の状態へ押し込む（CEF の生成可否が読む）。診断記録は別に残す
            // Push whether the host was skipped into WebUiHost's boot state (CEF creation reads it); the diagnostic record is kept separately
            Client.WebUiHost.Boot.WebUiHost.SetHostSkippedThisBoot(skipHost);
            if (skipHost)
            {
                Debug.LogWarning($"[WebUiHost] Editor限定スイッチによりWebホストを起動しません（uiSystem={uiSystem} skipMark={skipMarked}、Hub=nullで続行）");
                WebUiHealthRecorder.RecordHostStartup(WebUiHostStartupState.SkippedByEditorSwitch);
                return false;
            }
#endif
            // 終了時の購読はWebUiHostが一度だけ張る。命綱はUITKにあるので続行し、失敗はLogErrorで開発者へ届ける
            // WebUiHost owns its single shutdown subscription; the lifeline lives in UITK so boot continues, and failures reach developers as LogError
            // 外部プロセスの起動境界を隔離し、ゲームの初期化を続ける
            // Isolate external process startup failures and continue game initialization
            try
            {
                var started = await Client.WebUiHost.Boot.WebUiHost.StartAsync(exitToken);
                if (!started) Debug.LogError("[WebUiHost] 起動が完了しなかったため、Web UIなしで続行します");
                WebUiHealthRecorder.RecordHostStartup(started ? WebUiHostStartupState.Started : WebUiHostStartupState.Failed);
                return started;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Debug.LogError($"[WebUiHost] 外部プロセスまたはHTTP待受の起動に失敗したため、Web UIなしで続行します: {e}");
                WebUiHealthRecorder.RecordHostStartup(WebUiHostStartupState.Failed);
                return false;
            }
        }
    }
#if UNITY_EDITOR
    /// <summary>
    /// Editor限定: 次の1回の起動でWebホストを起動しない印。配布物には存在せず、非Editorコードから参照するとプレイヤービルドのコンパイルで落ちる
    /// Editor-only mark that skips the web host on the next boot; it does not exist in player builds, so a reference from non-editor code fails the player build
    /// 印はSessionStateに置き、起動時に読んだ時点で消費する（前例: PlaytestStartGateBypass）
    /// The mark lives in SessionState and is consumed when the boot reads it (precedent: PlaytestStartGateBypass)
    /// </summary>
    public static class WebUiHostEditorSkip
    {
        private const string SessionStateKey = "WebUiHostEditorSkip_NextBoot";

        // 未消費の印を次のPlayへ漏らさない
        // Never leak an unconsumed mark into the next play
        [UnityEditor.InitializeOnLoadMethod]
        private static void Initialize()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) UnityEditor.SessionState.EraseBool(SessionStateKey);
        }

        // 自動起動の入口から呼ぶ
        // Called from unattended entry points
        public static void Apply()
        {
            UnityEditor.SessionState.SetBool(SessionStateKey, true);
        }

        internal static bool ConsumeSkipMark()
        {
            var marked = UnityEditor.SessionState.GetBool(SessionStateKey, false);
            UnityEditor.SessionState.EraseBool(SessionStateKey);
            return marked;
        }
    }
#endif
}
