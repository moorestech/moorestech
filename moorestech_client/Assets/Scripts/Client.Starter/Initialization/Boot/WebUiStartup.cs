using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Initialization.Boot
{
    internal static class WebUiStartup
    {
        internal static async UniTask<bool> StartAsync(CancellationToken exitToken)
        {
#if UNITY_EDITOR
            // Editor限定スイッチの印があれば、起動失敗と同じHub=nullの経路で続ける
            // With the Editor-only switch marked, continue down the same hub-null path as a startup failure
            if (WebUiHostEditorSkip.ConsumeSkipMark())
            {
                Debug.LogWarning("[WebUiHost] Editor限定スイッチによりWebホストを起動しません（Hub=nullで続行）");
                return false;
            }
#endif
            // 終了時の購読はWebUiHostが一度だけ張る。UIはWeb一本なので起動失敗時は非表示で続ける
            // WebUiHost owns its single shutdown subscription; the web-only UI stays hidden if startup fails
            // 外部プロセスの起動境界を隔離し、ゲームの初期化を続ける
            // Isolate external process startup failures and continue game initialization
            try
            {
                var started = await Client.WebUiHost.Boot.WebUiHost.StartAsync(exitToken);
                if (!started) Debug.LogWarning("[WebUiHost] 起動が完了せず、遠隔実行も開きません");
                return started;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Debug.LogWarning($"[WebUiHost] 外部プロセスまたはHTTP待受の起動に失敗したため、Web UIなしで続行します: {e}");
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

        // 自動起動の入口（テストのPlayMode突入・今後のCEF無し起動ラダー）から呼ぶ
        // Called from unattended entry points (a test entering Play Mode, the future no-CEF boot ladder)
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
