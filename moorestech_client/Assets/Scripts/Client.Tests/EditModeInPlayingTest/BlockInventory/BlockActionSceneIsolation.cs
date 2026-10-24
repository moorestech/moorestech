#if UNITY_EDITOR
using Client.Common;
using Client.WebUiHost.Cef;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Callbacks;
using System;
using System.Reflection;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    internal static class BlockActionSceneIsolation
    {
        private const string SessionKey = "BlockActionSceneIsolation_WithoutBrowser";
        private const string OwnedSceneKey = "BlockActionSceneIsolation_OwnedScene";
        private const string OwnedRunKey = "BlockActionSceneIsolation_OwnedRun";
        private static string _currentRun;

        internal static void Prepare()
        {
            Assert.IsFalse(SessionState.GetBool(OwnedSceneKey, false), "the previous isolated play scene must be destroyed first");
            Assert.IsNotEmpty(_currentRun, "isolation requires an active Test Runner job");
            SessionState.SetString(OwnedRunKey, _currentRun);
            SessionState.SetBool(SessionKey, true);
        }

        internal static void Reset() => Finish("fixture teardown");

        [PostProcessScene]
        private static void IsolateBrowser()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(SessionKey, false)) return; // silent-ok: 対象起動以外は変更しない / 対象fixtureが隔離をassertする
            var browsers = Object.FindObjectsByType<WebUiCefNavigator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var navigator in browsers)
            {
                if (navigator.gameObject.scene.name != SceneConstant.MainGameSceneName) continue; // silent-ok: MainGameだけが所有対象 / 対象fixtureが存在数をassertする
                // 対象シーンで権限を消費し、次のロードへ渡さない
                // Consume permission in the owned scene instead of granting subsequent loads
                SessionState.SetBool(SessionKey, false);
                SessionState.SetBool(OwnedSceneKey, true);
                navigator.gameObject.SetActive(false);
                Debug.Log($"[BlockActionSceneIsolation] inactive before Awake: {navigator.name}");
            }
        }

        internal static void AssertBrowserIsInactive()
        {
            var browsers = Object.FindObjectsByType<WebUiCefNavigator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(browsers, Has.Length.EqualTo(1));
            Assert.That(browsers[0].gameObject.activeSelf, Is.False, "CEF must never activate in this binder/API test");
            Assert.IsFalse(SessionState.GetBool(SessionKey, false), "scene permission must be consumed");
            Assert.IsTrue(SessionState.GetBool(OwnedSceneKey, false), "the isolated scene must retain its cleanup owner");
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // このUTF版のCancelは公開完了通知を省くため、ジョブ終了通知を購読する
            // This pinned UTF skips public completion callbacks on cancel, so subscribe to job termination
            var progressEvent = typeof(TestRunnerApi).GetField("runProgressChanged", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var addListener = progressEvent.GetType().GetMethod("AddListener");
            var callback = typeof(BlockActionSceneIsolation).GetMethod(nameof(OnRunProgress), BindingFlags.Static | BindingFlags.NonPublic);
            var listener = Delegate.CreateDelegate(addListener.GetParameters()[0].ParameterType, callback);
            addListener.Invoke(progressEvent, new object[] { listener });
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Finish("editor reload");
        }

        private static void OnRunProgress(object progress)
        {
            var type = progress.GetType();
            _currentRun = (string)type.GetField("RunGuid").GetValue(progress);
            var finished = (bool)type.GetField("HasFinished").GetValue(progress);
            // 別ジョブから対象シーンの寿命を変更しない
            // Only the owning job can end this isolated scene's lifetime
            if (!finished || SessionState.GetString(OwnedRunKey, "") != _currentRun) return; // silent-ok: 他ジョブの進行は対象外 / 所有ジョブの終端通知とfixtureが検証する
            Finish("owning test job ended, including cancellation");
        }

        private static void OnBeforeReload()
        {
            // 正規のPlay突入リロードだけは準備印を引き継ぐ
            // Preserve preparation only across the expected play-entry reload
            if (EditorApplication.isPlaying || !EditorApplication.isPlayingOrWillChangePlaymode) Finish("assembly reload outside play entry");
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return; // silent-ok: Playシーン破棄後だけ所有を解除 / UnityのEnteredEditModeが完了境界
            // UnityがPlayシーンを破棄・元の編集シーンを復元してから解除する
            // Release ownership after Unity destroys play scenes and restores the editor scene
            Finish("entered edit mode after scene restoration");
        }

        private static void Finish(string reason)
        {
            var pending = SessionState.GetBool(SessionKey, false);
            var owned = SessionState.GetBool(OwnedSceneKey, false);
            if (!pending && !owned) return; // silent-ok: この2fixtureが所有しない起動は変更しない / 各fixtureが隔離状態を検査する
            Debug.Log($"[BlockActionSceneIsolation cleanup] {reason}; pending={pending}, owned={owned}, playing={EditorApplication.isPlaying}");
            SessionState.SetBool(SessionKey, false);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // inactiveのシーンを再利用せず、終了・破棄を先に要求する
                // Request play exit and scene destruction instead of reusing an inactive scene
                EditorApplication.isPlaying = false;
                return;
            }
            SessionState.SetBool(OwnedSceneKey, false);
            SessionState.EraseString(OwnedRunKey);
        }

    }
}
#endif
