using System;
using System.Reflection;
using Client.Game;
using Client.Playtest;
using Client.Playtest.Core;
using Client.Starter;
using Client.Starter.Editor;
using Common.Debug;
using Game.MapGeneration.Transfer;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;
using UnityEditor;
using UnityEngine;

namespace Client.Tests.Playtest
{
    public class PlaytestBootLifecycleTest
    {
        private const string DebugEnvironmentTypeKey = "DebugEnvironmentTypeKey";
        private string _previousDebugCacheOverride;

        [SetUp]
        public void SetUp()
        {
            _previousDebugCacheOverride = DebugParametersCacheDirectory.GetOverride();
            DebugParametersCacheDirectory.SetOverride(null);
            PlaytestWorldBootSession.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PlaytestBootLifecycle.HandlePlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
            DebugParametersCacheDirectory.SetOverride(_previousDebugCacheOverride);
        }

        [TestCase(WorldMapMode.Generated, 3, 0)]
        [TestCase(WorldMapMode.Template, 2, 12345)]
        public void PrepareWorldBootSession_mapMode別の隔離環境を構成する(string mapMode, int expectedEnvironmentType, int seed)
        {
            PlaytestBootLifecycle.PrepareWorldBootSession("/master/server_v8", "/tmp/fixed-world", mapMode, seed);

            var restored = PlaytestWorldBootSession.TryCreateInitializeProprieties(out var proprieties);
            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);

            // NoSave上書き停止、隔離cache、mode別環境、正式CLI値を一括で固定する
            // Pin disabled NoSave override, isolated cache, mode-specific environment, and official CLI values together
            Assert.That(SessionState.GetBool(SkipSaveLoadPlayModeSettings.SessionStateKey, true), Is.False);
            Assert.That(SessionState.GetBool("DebugObjectsBootstrap_Disabled", true), Is.False);
            Assert.That(DebugParametersCacheDirectory.GetOverride(), Is.EqualTo(PlaytestPaths.DebugCacheDirectory));
            Assert.That(DebugParameters.GetValueOrDefaultInt(DebugEnvironmentTypeKey, -1), Is.EqualTo(expectedEnvironmentType));
            Assert.That(DebugParameters.GetValueOrDefaultBool(DebugConst.SkitPlaySettingsKey), Is.True);
            Assert.That(restored, Is.True);
            Assert.That(settings.ServerDataDirectory, Is.EqualTo("/master/server_v8"));
            Assert.That(settings.WorldDirectory, Is.EqualTo("/tmp/fixed-world"));
            Assert.That(settings.MapMode, Is.EqualTo(mapMode));
            Assert.That(settings.Seed, Is.EqualTo(seed));
            Assert.That(settings.AutoSave, Is.False);
        }

        [Test]
        public void PrepareWorldBootSession_未知のmapModeをPlayMode前に拒否する()
        {
            SessionState.SetBool(SkipSaveLoadPlayModeSettings.SessionStateKey, true);
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            Assert.Throws<ArgumentException>(() =>
                PlaytestBootLifecycle.PrepareWorldBootSession("/master/server_v8", "/tmp/fixed-world", "unknown", 12345));

            // 不正入力は共通準備より前に拒否し、次の起動へ状態を残さない
            // Reject invalid input before common setup and leave no state for the next boot
            Assert.That(SessionState.GetBool(SkipSaveLoadPlayModeSettings.SessionStateKey, false), Is.True);
            Assert.That(SessionState.GetBool("DebugObjectsBootstrap_Disabled", true), Is.False);
            Assert.That(DebugParametersCacheDirectory.GetOverride(), Is.Null);
            Assert.That(PlaytestWorldBootSession.TryCreateInitializeProprieties(out _), Is.False);
            Assert.That(PlaytestBootLifecycle.RestoreAfterDomainReload(true), Is.False);
        }

        [Test]
        public void RestoreAfterDomainReload_固定起動だけをStart前に注入する()
        {
            PlaytestBootLifecycle.PrepareWorldBootSession("/master/server_v8", "/tmp/fixed-world", "generated", 67890);

            // domain reload後のhookと同じ入口でsceneLoaded購読を復元する
            // Restore the sceneLoaded subscription through the same entry used after domain reload
            var restoredHook = PlaytestBootLifecycle.RestoreAfterDomainReload(true);
            Assert.That(restoredHook, Is.True);
            Assert.That(PlaytestBootLifecycle.IsWorldBootSceneHookRegistered(), Is.True);
            Assert.That(PlaytestBootLifecycle.IsEnvironmentSceneHookRegistered(), Is.True);

            // 非アクティブobjectへ注入し、Startが走る前のproperty値を直接読む
            // Inject into an inactive object and inspect its property before Start can run
            var gameObject = new GameObject("PlaytestBootLifecycleTest");
            gameObject.SetActive(false);
            var pipeline = gameObject.AddComponent<InitializeScenePipeline>();
            var injected = PlaytestBootLifecycle.InjectWorldBootSettings(pipeline);
            var proprieties = GetInitializeProprieties(pipeline);
            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            UnityEngine.Object.DestroyImmediate(gameObject);

            Assert.That(injected, Is.True);
            Assert.That(PlaytestBootLifecycle.IsWorldBootSceneHookRegistered(), Is.False);
            Assert.That(PlaytestBootLifecycle.IsEnvironmentSceneHookRegistered(), Is.True);
            Assert.That(settings.WorldDirectory, Is.EqualTo("/tmp/fixed-world"));
            Assert.That(settings.MapMode, Is.EqualTo("generated"));
            Assert.That(settings.Seed, Is.EqualTo(67890));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PrepareLegacyBootSession_固定ワールド注入を購読しない(bool noSave)
        {
            // 既存2引数入口の準備経路がNoSave値だけを維持することを固定する
            // Pin that the legacy two-argument preparation preserves only its NoSave value
            PlaytestBootLifecycle.PrepareLegacyBootSession("/master/server_v8", noSave);

            var restoredBoot = PlaytestBootLifecycle.RestoreAfterDomainReload(true);
            var restoredWorld = PlaytestWorldBootSession.TryCreateInitializeProprieties(out _);

            Assert.That(SessionState.GetBool(SkipSaveLoadPlayModeSettings.SessionStateKey, !noSave), Is.EqualTo(noSave));
            Assert.That(SessionState.GetBool("DebugObjectsBootstrap_Disabled", false), Is.True);
            Assert.That(restoredBoot, Is.True);
            Assert.That(restoredWorld, Is.False);
            Assert.That(PlaytestBootLifecycle.IsWorldBootSceneHookRegistered(), Is.False);
            Assert.That(PlaytestBootLifecycle.IsEnvironmentSceneHookRegistered(), Is.False);
        }

        [Test]
        public void EnteredEditMode_起動状態と購読と隔離を完全に解除する()
        {
            PlaytestBootLifecycle.PrepareWorldBootSession("/master/server_v8", "/tmp/fixed-world", "generated", 12345);
            PlaytestBootLifecycle.RestoreAfterDomainReload(true);

            PlaytestBootLifecycle.HandlePlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
            var restoredWorld = PlaytestWorldBootSession.TryCreateInitializeProprieties(out _);

            Assert.That(restoredWorld, Is.False);
            Assert.That(PlaytestBootLifecycle.IsWorldBootSceneHookRegistered(), Is.False);
            Assert.That(PlaytestBootLifecycle.IsEnvironmentSceneHookRegistered(), Is.False);
            Assert.That(DebugParametersCacheDirectory.GetOverride(), Is.Null);
            Assert.That(SessionState.GetBool(SkipSaveLoadPlayModeSettings.SessionStateKey, true), Is.False);
            Assert.That(SessionState.GetBool("DebugObjectsBootstrap_Disabled", true), Is.False);
        }

        [Test]
        public void PublicEntrypoints_中央準備処理へ委譲する()
        {
            // 公開入口から中央準備処理への橋を削る退行をIL呼び出しで検出する
            // Detect removal of the bridge from public entrypoints to central preparation via IL calls
            var legacyEntry = typeof(PlaytestBoot).GetMethod(nameof(PlaytestBoot.PrepareAndEnterPlayMode));
            var worldEntry = typeof(PlaytestBoot).GetMethod(nameof(PlaytestBoot.PrepareWorldAndEnterPlayMode));
            var legacyPreparation = typeof(PlaytestBootLifecycle).GetMethod("PrepareLegacyBootSession", BindingFlags.Static | BindingFlags.NonPublic);
            var worldPreparation = typeof(PlaytestBootLifecycle).GetMethod("PrepareWorldBootSession", BindingFlags.Static | BindingFlags.NonPublic);
            var commonPreparation = typeof(PlaytestBootLifecycle).GetMethod("PrepareCommonBootSession", BindingFlags.Static | BindingFlags.NonPublic);
            var fixedWorldDebugSettings = typeof(PlaytestBootLifecycle).GetMethod("ConfigureFixedWorldDebugSettings", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(MethodCallInspector.ContainsCall(legacyEntry, legacyPreparation), Is.True);
            Assert.That(MethodCallInspector.ContainsCall(worldEntry, worldPreparation), Is.True);
            Assert.That(MethodCallInspector.ContainsCall(worldPreparation, fixedWorldDebugSettings), Is.True);
            Assert.That(MethodCallInspector.CallsInOrder(worldPreparation, commonPreparation, fixedWorldDebugSettings), Is.True);
            Assert.That(MethodCallInspector.ContainsCall(legacyPreparation, fixedWorldDebugSettings), Is.False);
        }

        [Test]
        public void EditorCallbacks_中央復元と注入処理へ委譲する()
        {
            // domain reloadとsceneLoadedの実callbackがテスト済み処理へ繋がることを固定する
            // Pin that real domain-reload and sceneLoaded callbacks reach the tested lifecycle methods
            var domainReloadHook = typeof(PlaytestBoot).GetMethod("HookAfterDomainReload", BindingFlags.Static | BindingFlags.NonPublic);
            var restore = typeof(PlaytestBootLifecycle).GetMethod("RestoreAfterDomainReload", BindingFlags.Static | BindingFlags.NonPublic);
            var sceneLoaded = typeof(PlaytestBootLifecycle).GetMethod("HandleWorldBootSceneLoaded", BindingFlags.Static | BindingFlags.NonPublic);
            var inject = typeof(PlaytestBootLifecycle).GetMethod("InjectWorldBootSettings", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(MethodCallInspector.ContainsCall(domainReloadHook, restore), Is.True);
            Assert.That(MethodCallInspector.ContainsCall(sceneLoaded, inject), Is.True);
        }

        private static InitializeProprieties GetInitializeProprieties(InitializeScenePipeline pipeline)
        {
            var field = typeof(InitializeScenePipeline).GetField("_proprieties", BindingFlags.Instance | BindingFlags.NonPublic);
            return (InitializeProprieties)field.GetValue(pipeline);
        }
    }
}
