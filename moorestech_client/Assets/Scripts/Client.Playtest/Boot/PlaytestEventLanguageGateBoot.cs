using System;
using Client.Common;
using Client.Playtest.Core;
using Client.Starter.EventMode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Playtest.Boot
{
    /// <summary>
    /// 出展モードで起動して言語ゲートを通る録画の起動口。ゲートで初期化が止まるので MainGame の読み込みで ready を書く
    /// Boot entry for recordings that start in exhibition mode and pass the language gate; the gate holds initialization, so ready is written on MainGame load
    /// </summary>
    /// <remarks>
    /// 出展モードの環境変数は Play の間だけ立て、Edit へ戻ったら前の値へ戻す。ワールドを消す自動開始は Play の最初のシーンがメインメニューのときだけ走り、この起動は GameInitialaizer から入るので走らない
    /// The exhibition env vars stand only during Play and return to their prior values on returning to Edit; the world-wiping auto start runs only when Play's first scene is the main menu, and this boot enters from GameInitialaizer
    /// </remarks>
    public static class PlaytestEventLanguageGateBoot
    {
        private const string ArmedKey = "PlaytestEventLanguageGateBoot_Armed";
        private const string AppliedKey = "PlaytestEventLanguageGateBoot_Applied";
        private const string SavedValuePrefix = "PlaytestEventLanguageGateBoot_Value_";
        private const string SavedPresencePrefix = "PlaytestEventLanguageGateBoot_Had_";
        // 録画の間に無操作終了が走らない長さ（既定の 180 秒は録画より短くなりうる）
        // Long enough that no idle quit fires during the recording (the 180 s default can be shorter than a recording)
        private const string RecordingIdleTimeoutSeconds = "3600";

        private static readonly string[] OverriddenKeys = { EventExhibitionSettings.EnableEnvKey, EventExhibitionSettings.EditorOptInEnvKey, EventExhibitionSettings.IdleTimeoutEnvKey };

        // 次の PlaytestBoot の起動 1 回だけを出展モードにする（run-scenario.sh より前に execute-dynamic-code で呼ぶ）
        // Puts only the next PlaytestBoot boot into exhibition mode (call via execute-dynamic-code before run-scenario.sh)
        public static string ArmForNextBoot()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "ERROR: already playing";
            SessionState.SetBool(ArmedKey, true);
            return "exhibition-armed";
        }

        public static string Disarm()
        {
            SessionState.SetBool(ArmedKey, false);
            return "disarmed";
        }

        // PlaytestBoot のドメインリロード後に呼ぶ。武装していれば 1 回だけ環境変数を立て、MainGame の読み込みで ready を書く
        // Called after PlaytestBoot's domain reload; when armed, sets the env vars once and writes ready on MainGame load
        internal static bool ApplyIfArmed()
        {
            if (!SessionState.GetBool(ArmedKey, false)) return false;
            SessionState.SetBool(ArmedKey, false);

            SaveAndOverrideEnvironment();
            SceneManager.sceneLoaded += WriteReadyOnMainGameLoaded;
            Debug.Log("[PlaytestEventLanguageGateBoot] this boot runs in exhibition mode; ready is written when MainGame loads");
            return true;

            #region Internal

            void SaveAndOverrideEnvironment()
            {
                foreach (var key in OverriddenKeys)
                {
                    var previous = Environment.GetEnvironmentVariable(key);
                    SessionState.SetBool(SavedPresencePrefix + key, previous != null);
                    SessionState.SetString(SavedValuePrefix + key, previous ?? string.Empty);
                }
                Environment.SetEnvironmentVariable(EventExhibitionSettings.EnableEnvKey, EventExhibitionSettings.EnabledValue);
                Environment.SetEnvironmentVariable(EventExhibitionSettings.EditorOptInEnvKey, EventExhibitionSettings.EnabledValue);
                Environment.SetEnvironmentVariable(EventExhibitionSettings.IdleTimeoutEnvKey, RecordingIdleTimeoutSeconds);
                SessionState.SetBool(AppliedKey, true);
            }

            #endregion
        }

        private static void WriteReadyOnMainGameLoaded(Scene scene, LoadSceneMode mode)
        {
            // silent-ok: MainGame 以外のシーンの読み込みは待つ対象でない / MainGame が来なければ run-scenario.sh の ready 待ちが時間切れで NG を出す
            if (scene.name != SceneConstant.MainGameSceneName) return;
            SceneManager.sceneLoaded -= WriteReadyOnMainGameLoaded;
            PlaytestPaths.WriteReadyMarker();
        }

        [InitializeOnLoadMethod]
        private static void HookRestoreOnEditMode()
        {
            EditorApplication.playModeStateChanged -= RestoreOnEditMode;
            EditorApplication.playModeStateChanged += RestoreOnEditMode;
        }

        // Edit へ戻ったら立てる前の値へ戻す（残すと開発機の次の Play が出展モードで始まる）
        // On returning to Edit, restore the prior values (leaving them would start the developer's next Play in exhibition mode)
        private static void RestoreOnEditMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            // silent-ok: この起動口を使わなかった Play では戻すものが無い / 使った Play は AppliedKey が真
            if (!SessionState.GetBool(AppliedKey, false)) return;
            foreach (var key in OverriddenKeys)
            {
                var hadValue = SessionState.GetBool(SavedPresencePrefix + key, false);
                Environment.SetEnvironmentVariable(key, hadValue ? SessionState.GetString(SavedValuePrefix + key, string.Empty) : null);
            }
            SessionState.SetBool(AppliedKey, false);
        }
    }
}
