using System;
using System.IO;
using Client.Starter.EventMode;
using Game.Paths;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// 出展モードの自動開始を本物の起動フックで走らせるための前後処理。自動開始は既定ワールドを消すので、開発機のワールドを同じSavesの退避名へ動かして守る。
    /// 状態はドメインリロードを跨ぐため、復元に要る値はSessionStateに置く。前回の中断で退避名が残っていれば上書きせずに止める。
    /// Set-up and tear-down for running the exhibition auto start through its real boot hook. The auto start wipes the default world, so the developer's world is moved to a backup name inside the same Saves and protected.
    /// The state crosses domain reloads, so what restoring needs lives in SessionState; a backup left by an earlier interrupted run stops the test instead of being overwritten.
    /// </summary>
    public static class EventModeAutoStartBootEnvironment
    {
        private const string MainMenuScenePath = "Assets/Scenes/Game/MainMenu.unity";
        private const string BackupSuffix = ".event-mode-autostart-test-backup";
        private const string PreviousStartScenePathKey = "EventModeAutoStartBootEnvironment_PreviousStartScenePath";
        private const string PreviousEnvValuePrefix = "EventModeAutoStartBootEnvironment_PreviousEnv_";
        private const string UnsetMarker = "\u0000unset";

        private static readonly string[] EventModeEnvKeys = { EventExhibitionSettings.EnableEnvKey, EventExhibitionSettings.EditorOptInEnvKey };

        private static string BackupDirectory => GameSystemPaths.DefaultWorldDirectory + BackupSuffix;

        // Play突入前に呼ぶ。起動シーンをMainMenuにし、出展モードを有効にし、開発機のワールドを退避する
        // Call before entering Play: start from MainMenu, enable exhibition mode and move the developer's world aside
        public static void Prepare()
        {
            // 退避名が残っているのは前回の中断。開発機のワールドの可能性があるので上書きしない
            // A leftover backup means an earlier interrupted run; it may hold the developer's world, so it is never overwritten
            Assert.IsFalse(Directory.Exists(BackupDirectory), $"前回の中断で退避ワールドが残っている。中身を確認して手で戻すか消すこと: {BackupDirectory}");
            if (Directory.Exists(GameSystemPaths.DefaultWorldDirectory)) Directory.Move(GameSystemPaths.DefaultWorldDirectory, BackupDirectory);

            // 起動フック（AfterSceneLoad）がMainMenuで走るよう、開いているシーンに依存せず起動シーンを固定する
            // Pin the start scene regardless of the open scene so the AfterSceneLoad hook runs in MainMenu
            var previousStartScene = EditorSceneManager.playModeStartScene;
            SessionState.SetString(PreviousStartScenePathKey, previousStartScene == null ? "" : AssetDatabase.GetAssetPath(previousStartScene));
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);

            // 環境変数はプロセスに残るためドメインリロード後の起動フックからも読める
            // Env vars live in the process, so the boot hook reads them after the domain reload
            foreach (var key in EventModeEnvKeys)
            {
                SessionState.SetString(PreviousEnvValuePrefix + key, Environment.GetEnvironmentVariable(key) ?? UnsetMarker);
                Environment.SetEnvironmentVariable(key, "1");
            }
        }

        // Play終了後に呼ぶ。テストが作ったワールドを捨てて開発機のワールドを戻し、起動シーンと環境変数を元へ戻す
        // Call after leaving Play: drop the world the test made, bring the developer's world back and restore the start scene and env vars
        public static void Restore()
        {
            foreach (var key in EventModeEnvKeys)
            {
                var previous = SessionState.GetString(PreviousEnvValuePrefix + key, UnsetMarker);
                Environment.SetEnvironmentVariable(key, previous == UnsetMarker ? null : previous);
                SessionState.EraseString(PreviousEnvValuePrefix + key);
            }

            var previousStartScenePath = SessionState.GetString(PreviousStartScenePathKey, "");
            EditorSceneManager.playModeStartScene = previousStartScenePath == "" ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previousStartScenePath);
            SessionState.EraseString(PreviousStartScenePathKey);

            // 退避が無い（元々ワールドが無かった）場合も、テストが作ったワールドは残さない
            // Even without a backup (no world existed), the world the test created is not left behind
            GameSystemPaths.DeleteDefaultWorldDirectory();
            if (Directory.Exists(BackupDirectory)) Directory.Move(BackupDirectory, GameSystemPaths.DefaultWorldDirectory);
        }
    }
}
