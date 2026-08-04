using System.Reflection;
using Client.Playtest;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Client.Tests.Playtest
{
    public class PlaytestBootUnityCallbackBindingTest
    {
        [Test]
        public void HookAfterDomainReload_UnityInitializeOnLoadから起動される()
        {
            // Unity起動属性を削除してcallbackが沈黙する退行を検出する
            // Detect regressions where removing Unity's initialization attribute silences the callback
            var hook = typeof(PlaytestBoot).GetMethod("HookAfterDomainReload", BindingFlags.Static | BindingFlags.NonPublic);
            var attribute = hook.GetCustomAttribute<InitializeOnLoadMethodAttribute>();

            Assert.That(attribute, Is.Not.Null);
        }

        [Test]
        public void RestoreAfterDomainReload_SceneLoadedイベントへ実際に登録する()
        {
            // 内部boolではなくUnity eventのadd accessor呼び出しを直接固定する
            // Pin the actual Unity event add-accessor call instead of trusting an internal boolean
            var restore = typeof(PlaytestBootLifecycle).GetMethod("RestoreAfterDomainReload", BindingFlags.Static | BindingFlags.NonPublic);
            var addSceneLoaded = typeof(SceneManager).GetEvent(nameof(SceneManager.sceneLoaded)).GetAddMethod();

            Assert.That(MethodCallInspector.ContainsCall(restore, addSceneLoaded), Is.True);
        }
    }
}
