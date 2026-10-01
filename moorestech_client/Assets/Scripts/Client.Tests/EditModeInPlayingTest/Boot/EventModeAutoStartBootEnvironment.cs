using System;
using System.IO;
using System.Linq;
using Client.Tests.EventMode;
using Game.Paths;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// 出展モードの自動開始を本物の起動フックで走らせるための前後処理。自動開始は既定ワールドを消すので、既定ワールドの置き場を起動環境の上書きキーで一時ディレクトリへ向け、開発機のSaves/world_1には触れない。
    /// 状態はドメインリロードを跨ぐため、復元に要る値はSessionStateに置く。
    /// Set-up and tear-down for running the exhibition auto start through its real boot hook. The auto start wipes the default world, so the default world is pointed at a temporary directory through the launch-environment override key and the developer's Saves/world_1 is never touched.
    /// The state crosses domain reloads, so what restoring needs lives in SessionState.
    /// </summary>
    public static class EventModeAutoStartBootEnvironment
    {
        private const string MainMenuScenePath = "Assets/Scenes/Game/MainMenu.unity";
        private const string PreviousStartScenePathKey = "EventModeAutoStartBootEnvironment_PreviousStartScenePath";
        private const string PreviousEnvValuePrefix = "EventModeAutoStartBootEnvironment_PreviousEnv_";
        private const string TemporaryWorldDirectoryKey = "EventModeAutoStartBootEnvironment_TemporaryWorldDirectory";
        private const string PreparedMarkerKey = "EventModeAutoStartBootEnvironment_Prepared";

        // 出展モードの有効化キーと既定ワールドの置き場キーを、まとめて退避・復元する
        // Save and restore the exhibition enabling keys together with the default world location key
        private static readonly string[] SavedEnvKeys = EventModeTestEnvironment.ExhibitionEnableKeys.Append(GameSystemPaths.DefaultWorldDirectoryOverrideEnvKey).ToArray();

        // Prepareが既定ワールドの置き場として向けた一時ディレクトリ
        // The temporary directory Prepare pointed the default world at
        public static string TemporaryWorldDirectory => SessionState.GetString(TemporaryWorldDirectoryKey, "");

        // Play突入前に呼ぶ。起動シーンをMainMenuにし、既定ワールドを一時ディレクトリへ向け、出展モードを有効にする
        // Call before entering Play: start from MainMenu, point the default world at a temporary directory and enable exhibition mode
        public static void Prepare()
        {
            // 戻す値を置いてから印を立てる。印の無いRestoreは環境を書き換えない
            // Store what to restore before marking; a Restore without the mark rewrites nothing
            EventModeTestEnvironment.SaveToSession(PreviousEnvValuePrefix, SavedEnvKeys);
            var previousStartScene = EditorSceneManager.playModeStartScene;
            SessionState.SetString(PreviousStartScenePathKey, previousStartScene == null ? "" : AssetDatabase.GetAssetPath(previousStartScene));
            var temporaryWorldDirectory = Path.Combine(Path.GetTempPath(), $"moorestech_event_mode_autostart_test_{Guid.NewGuid()}");
            SessionState.SetString(TemporaryWorldDirectoryKey, temporaryWorldDirectory);
            SessionState.SetBool(PreparedMarkerKey, true);

            // 起動フック（AfterSceneLoad）がMainMenuで走るよう、開いているシーンに依存せず起動シーンを固定する
            // Pin the start scene regardless of the open scene so the AfterSceneLoad hook runs in MainMenu
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);

            // 環境変数はプロセスに残るため、ドメインリロード後の起動フックと内蔵サーバーからも読める
            // Env vars live in the process, so the boot hook and the embedded server read them after the domain reload
            Environment.SetEnvironmentVariable(GameSystemPaths.DefaultWorldDirectoryOverrideEnvKey, temporaryWorldDirectory);
            EventModeTestEnvironment.EnableExhibitionMode();
        }

        // Play終了後に呼ぶ。環境変数と起動シーンを戻し、テストが使った一時ワールドを消す
        // Call after leaving Play: restore the env vars and start scene, then remove the temporary world the test used
        public static void Restore()
        {
            // Prepareが印を立てる前に落ちた場合は戻す値が無いので、開発者の環境を書き換えない
            // If Prepare failed before marking there is nothing to restore, so the developer's environment is left as it is
            if (!SessionState.GetBool(PreparedMarkerKey, false))
            {
                Debug.Log("EventModeAutoStartBootEnvironment: Restore skipped because Prepare did not finish storing the values to restore");
                return;
            }
            SessionState.EraseBool(PreparedMarkerKey);

            EventModeTestEnvironment.RestoreFromSession(PreviousEnvValuePrefix, SavedEnvKeys);
            var previousStartScenePath = SessionState.GetString(PreviousStartScenePathKey, "");
            EditorSceneManager.playModeStartScene = previousStartScenePath == "" ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previousStartScenePath);
            SessionState.EraseString(PreviousStartScenePathKey);

            var temporaryWorldDirectory = SessionState.GetString(TemporaryWorldDirectoryKey, "");
            SessionState.EraseString(TemporaryWorldDirectoryKey);
            if (Directory.Exists(temporaryWorldDirectory)) Directory.Delete(temporaryWorldDirectory, true);
        }
    }
}
