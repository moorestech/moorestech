using System;
using System.Reflection;
using Client.Playtest;
using Client.PlaytestReceiver.Launch;
using Client.Starter;
using Client.Starter.Playtest.TitleGates;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Client.Tests.Playtest
{
    public class PlaytestBootUnityCallbackBindingTest
    {
        // 直接起動の漏斗が識別公開を先に通ることを本番の非同期入口で固定する（C4/C15）
        // Pin publication before the direct boot funnel in the production async entry (C4/C15)
        [Test]
        public void InitializeScenePipeline_漏斗前に識別を公開する()
        {
            Type stateMachine = null;
            foreach (var nestedType in typeof(InitializeScenePipeline).GetNestedTypes(BindingFlags.NonPublic))
            {
                if (nestedType.Name.StartsWith("<Initialize>d__", StringComparison.Ordinal)) stateMachine = nestedType;
            }

            var moveNext = stateMachine?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic);
            var publish = typeof(PlaytestLaunchProfile).GetMethod(nameof(PlaytestLaunchProfile.EnsureIdentityPublished));
            var evaluate = typeof(PlaytestTitleGates).GetMethod(nameof(PlaytestTitleGates.EvaluateStart));

            Assert.That(moveNext, Is.Not.Null);
            Assert.That(MethodCallInspector.CallsInOrder(moveNext, publish, evaluate), Is.True,
                "直接起動の漏斗が識別公開より先に進み、異常終了箱へ未確定理由を残す");
        }

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
