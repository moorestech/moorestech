#if UNITY_EDITOR
using System;
using System.Linq;
using CefUnity.Runtime;
using Client.WebUiHost.Cef;
using Client.Common;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    internal static class BlockActionSceneFixture
    {
        private const string MainGamePath = "Assets/Scenes/Game/MainGame.unity";
        private const string SetupKey = "BlockActionSceneIsolation_BuildScenes";
        private const string FolderKey = "BlockActionSceneIsolation_FixtureFolder";

        internal static void Prepare()
        {
            // 元のビルドシーン一覧をリロード越しに復元できるよう保管する
            // Preserve the original build-scene list across domain reloads
            Assert.IsEmpty(SessionState.GetString(SetupKey, ""), "a previous scene fixture still owns the build settings");
            var scenes = EditorBuildSettings.scenes;
            var setup = new SceneSetupSnapshot { Paths = scenes.Select(s => s.path).ToArray(), Enabled = scenes.Select(s => s.enabled).ToArray() };
            SessionState.SetString(SetupKey, JsonUtility.ToJson(setup));
            var folder = "BlockActionSceneIsolation_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder);
            var folderPath = "Assets/" + folder;
            SessionState.SetString(FolderKey, folderPath);

            // Editor内で複製を作り、native componentを起動前に取り除く
            // Remove the native component from an editor-created copy before play entry
            var fixturePath = folderPath + "/MainGame.unity";
            Assert.IsTrue(AssetDatabase.CopyAsset(MainGamePath, fixturePath));
            var scene = EditorSceneManager.OpenScene(fixturePath, OpenSceneMode.Additive);
            var navigator = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WebUiCefNavigator>(true)).Single();
            navigator.gameObject.SetActive(false);
            if (PrefabUtility.IsPartOfPrefabInstance(navigator.gameObject))
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(navigator.gameObject), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var browser = navigator.GetComponent<CefUnityBrowserSample>();
            Assert.IsNotNull(browser, "source scene must contain its normal native browser");
            Object.DestroyImmediate(browser);

            // 描画用環境に頼らず、API検査中のプレイヤーを足場で支える
            // Support the player during API checks independently of authored rendering environments
            var ground = new GameObject("NonRenderingApiTestFloor");
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.layer = LayerConst.GroundLayer;
            var collider = ground.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, -0.5f, 0);
            collider.size = new Vector3(128, 1, 128);
            Assert.IsTrue(EditorSceneManager.SaveScene(scene));
            Assert.IsTrue(EditorSceneManager.CloseScene(scene, true));

            // 同名MainGameの解決先だけを一時fixtureへ切り替える
            // Route only the MainGame build-scene entry to the temporary fixture
            Assert.AreEqual(1, scenes.Count(s => s.path == MainGamePath));
            EditorBuildSettings.scenes = scenes.Select(s => s.path == MainGamePath ? new EditorBuildSettingsScene(fixturePath, s.enabled) : s).ToArray();
            Debug.Log("[BlockActionSceneFixture] prepared without native CEF: " + fixturePath);
        }

        internal static void Restore()
        {
            var saved = SessionState.GetString(SetupKey, "");
            if (string.IsNullOrEmpty(saved)) return; // silent-ok: 対象fixtureが存在しない起動は変更しない / 対象テストと通常起動の検証で確認する
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode, "destroy the isolated play scene before restoring build settings");
            var setup = JsonUtility.FromJson<SceneSetupSnapshot>(saved);
            EditorBuildSettings.scenes = setup.Paths.Select((path, index) => new EditorBuildSettingsScene(path, setup.Enabled[index])).ToArray();

            // Playシーン破棄後に複製だけを削除し、元シーンは保存しない
            // Delete only the copy after play scenes are destroyed; never save the original
            var folder = SessionState.GetString(FolderKey, "");
            for (var i = SceneManager.sceneCount - 1; 0 <= i; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.path.StartsWith(folder + "/", StringComparison.Ordinal)) EditorSceneManager.CloseScene(scene, true);
            }
            Assert.IsTrue(AssetDatabase.DeleteAsset(folder), "temporary isolation scene could not be deleted: " + folder);
            SessionState.EraseString(SetupKey);
            SessionState.EraseString(FolderKey);
            Debug.Log("[BlockActionSceneFixture] restored original build scenes and deleted " + folder);
        }

        [Serializable]
        private sealed class SceneSetupSnapshot
        {
            public string[] Paths;
            public bool[] Enabled;
        }
    }
}
#endif
