using Client.WebUiHost.Cef;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    internal static class BlockActionSceneIsolation
    {
        private const string SessionKey = "BlockActionSceneIsolation_WithoutBrowser";

        internal static void Prepare()
        {
            SessionState.SetBool(SessionKey, true);
        }

        internal static void Reset()
        {
            SessionState.SetBool(SessionKey, false);
        }

        [PostProcessScene]
        private static void IsolateBrowser()
        {
            if (!SessionState.GetBool(SessionKey, false)) return; // silent-ok: 対象テスト以外は処理しない / 各テストが隔離状態を検査する
            // 描画外のAPI試験では有効化前にブラウザを隔離する
            // Isolate the browser before activation in non-rendering API tests
            foreach (var navigator in Object.FindObjectsByType<WebUiCefNavigator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                navigator.gameObject.SetActive(false);
                Debug.Log($"[BlockActionSceneIsolation] inactive before Awake: {navigator.name}");
            }
        }

        internal static void AssertBrowserIsInactive()
        {
            var browsers = Object.FindObjectsByType<WebUiCefNavigator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(browsers, Has.Length.EqualTo(1));
            Assert.That(browsers[0].gameObject.activeSelf, Is.False, "CEF must never activate in this binder/API test");
        }
    }
}
