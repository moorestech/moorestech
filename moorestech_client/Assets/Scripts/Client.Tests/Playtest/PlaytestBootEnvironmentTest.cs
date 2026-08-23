using Client.DebugSystem.Environment;
using Client.Playtest;
using Game.MapGeneration.Transfer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Client.Tests.Playtest
{
    public class PlaytestBootEnvironmentTest
    {
        [TearDown]
        public void TearDown()
        {
            PlaytestBootLifecycle.HandlePlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
            DestroyRoot<DebugEnvironmentObjectRoot>();
            DestroyRoot<PureNatureEnvironmentObjectRoot>();
            DestroyRoot<OtherEnvironmentObjectRoot>();
        }

        [Test]
        public void Generated固定起動_MainGame初期化前に全authored環境を無効化する()
        {
            // MainGameシーンロード直後と同じ構成を作り、地形構築前の環境適用を検証する
            // Recreate the state right after MainGame loads and verify environment application before terrain building
            CreateActiveRoot<DebugEnvironmentObjectRoot>();
            CreateActiveRoot<PureNatureEnvironmentObjectRoot>();
            CreateActiveRoot<OtherEnvironmentObjectRoot>();
            PlaytestBootLifecycle.PrepareWorldBootSession(
                "/master/server_v8", "/tmp/fixed-world", WorldMapMode.Generated, 12345);

            var applied = PlaytestBootLifecycle.ApplyFixedWorldEnvironment();

            Assert.That(applied, Is.True);
            Assert.That(Object.FindFirstObjectByType<DebugEnvironmentObjectRoot>(FindObjectsInactive.Include).gameObject.activeSelf, Is.False);
            Assert.That(Object.FindFirstObjectByType<PureNatureEnvironmentObjectRoot>(FindObjectsInactive.Include).gameObject.activeSelf, Is.False);
            Assert.That(Object.FindFirstObjectByType<OtherEnvironmentObjectRoot>(FindObjectsInactive.Include).gameObject.activeSelf, Is.False);
        }

        private static void CreateActiveRoot<TRoot>() where TRoot : Component
        {
            new GameObject(typeof(TRoot).Name).AddComponent<TRoot>();
        }

        private static void DestroyRoot<TRoot>() where TRoot : Component
        {
            var root = Object.FindFirstObjectByType<TRoot>(FindObjectsInactive.Include);
            if (root != null) Object.DestroyImmediate(root.gameObject);
        }
    }
}
