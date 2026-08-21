using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Tutorial;
using Client.Localization;
using Client.Starter;
using Client.Tests.Playtest;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests
{
    /// <summary>
    /// ゲームが正常に起動できるかを検証する統合テスト。
    ///
    /// 【重要: unity-test.sh (CliTestRunner) からは実行できません】
    /// このテストは EnterPlayMode を使用しており、ドメインリロードが発生します。
    /// CliTestRunner は runSynchronously = true で動作するため、ドメインリロード時に
    /// ResultCallbacks インスタンスが破棄され、テスト結果が 0件（passed: 0, failed: 0）として報告されます。
    ///
    /// 実行方法: Unity エディタの Test Runner ウィンドウ (Window > General > Test Runner) から手動実行してください。
    /// </summary>
    public class StartGameTest
    {
        [UnityTest]
        public IEnumerator StartGameCheckTest()
        {
            // テスト中はデバッグオブジェクトの生成を無効化（ドメインリロード後も保持される）
            // Disable debug object creation during test (persists across domain reload).
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", true);

            // 前回のテストで解放されなかったAssetBundleをプレイモード前にクリーンアップ
            // Clean up stale AssetBundles before entering play mode to avoid conflicts.
            AssetBundle.UnloadAllAssetBundles(true);

            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return SetUp().ToCoroutine();

            yield return new ExitPlayMode();

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask SetUp()
            {
                var initialDictionaryRevision = Localize.GetDictionaryRevision();
                var loadTask = LoadMainGame();
                var timeOuter = UniTask.Delay(TimeSpan.FromSeconds(30));

                var result = await UniTask.WhenAny(loadTask, timeOuter);
                if (result == 1)
                {
                   Assert.Fail("LoadMainGame timed out.");
                }

                // 起動後の辞書世代更新を待つ
                // Wait until the production boot path composes mod dictionaries and publishes a new revision
                for (var i = 0; i < 300 && Localize.GetDictionaryRevision() <= initialDictionaryRevision; i++)
                {
                    await UniTask.Delay(TimeSpan.FromMilliseconds(100));
                }

                Assert.That(Localize.GetDictionaryRevision(), Is.GreaterThan(initialDictionaryRevision));
            }

            #endregion
        }

        [Test]
        public void InitializeScenePipeline_ゲーム辞書合成を起動経路に含む()
        {
            Type initializeStateMachine = null;
            foreach (var nestedType in typeof(InitializeScenePipeline).GetNestedTypes(BindingFlags.NonPublic))
            {
                if (nestedType.Name.StartsWith("<Initialize>d__", StringComparison.Ordinal)) initializeStateMachine = nestedType;
            }

            // 起動本体と辞書合成を直結する
            // Pin the direct call from the async MoveNext body to game dictionary composition
            var moveNext = initializeStateMachine?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic);
            var composerType = typeof(InitializeScenePipeline).Assembly.GetType("Client.Starter.Initialization.GameDictionaryComposer");
            var compose = composerType?.GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(initializeStateMachine, Is.Not.Null);
            Assert.That(moveNext, Is.Not.Null);
            Assert.That(compose, Is.Not.Null);
            Assert.That(MethodCallInspector.ContainsCall(moveNext, compose), Is.True);
        }

        [Test]
        public void MainGameScene_ローカライズ配線と鉱脈表示基盤が共存する()
        {
            const string scenePath = "Assets/Scenes/Game/MainGame.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                // 両系統のシーン要素を同時検証する
                // Verify scene elements from both merge parents together because either side is easy to drop
                var expectedKeys = new HashSet<string>
                {
                    "ui.blueprint.nameInputConfirm",
                    "ui.blueprint.nameInputPlaceholder",
                    "ui.common.cancel",
                    "ui.blueprint.nameInputTitle",
                };
                var actualKeys = new HashSet<string>();
                var localizedTexts = UnityEngine.Object.FindObjectsByType<TextMeshProLocalize>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                foreach (var localizedText in localizedTexts)
                {
                    if (localizedText.gameObject.scene != scene) continue;
                    var serializedText = new SerializedObject(localizedText);
                    actualKeys.Add(serializedText.FindProperty("key").stringValue);
                }

                var veinDatastores = UnityEngine.Object.FindObjectsByType<OutcropGameObjectDatastore>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var sceneHasVeinDatastore = false;
                foreach (var veinDatastore in veinDatastores)
                {
                    if (veinDatastore.gameObject.scene == scene) sceneHasVeinDatastore = true;
                }

                Assert.That(actualKeys.IsSupersetOf(expectedKeys), Is.True);
                Assert.That(sceneHasVeinDatastore, Is.True);

                // 両pin配線と欠損を検証
                // Verify both pin wiring and absence
                var mapPins = FindSceneComponents<MapObjectPin>(scene);
                var veinPins = FindSceneComponents<VeinPin>(scene);
                var starters = FindSceneComponents<MainGameStarter>(scene);
                Assert.AreEqual(1, mapPins.Count);
                Assert.AreEqual(1, veinPins.Count);
                Assert.AreNotSame(mapPins[0].gameObject, veinPins[0].gameObject);
                Assert.IsFalse(veinPins[0].gameObject.activeSelf);
                Assert.AreEqual(1, starters.Count);

                var serializedStarter = new SerializedObject(starters[0]);
                Assert.AreSame(mapPins[0], serializedStarter.FindProperty("mapObjectPin").objectReferenceValue);
                Assert.AreSame(veinPins[0], serializedStarter.FindProperty("veinPin").objectReferenceValue);
                foreach (var root in scene.GetRootGameObjects())
                    Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root), root.name);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            #region Internal

            List<T> FindSceneComponents<T>(Scene targetScene) where T : Component
            {
                var result = new List<T>();
                foreach (var component in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (component.gameObject.scene == targetScene) result.Add(component);
                }
                return result;
            }

            #endregion
        }
    }
}
