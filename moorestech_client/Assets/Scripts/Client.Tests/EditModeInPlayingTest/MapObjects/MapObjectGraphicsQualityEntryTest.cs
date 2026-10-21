using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Client.Common;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.Map.MapObject;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.MapModule;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.MapData;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;
using Object = UnityEngine.Object;

namespace Client.Tests.EditModeInPlayingTest.MapObjects
{
    [Category("CiShardClientNearFieldStartup")]
    public class MapObjectGraphicsQualityEntryTest
    {
        [UnityTearDown]
        public IEnumerator RestorePlayModeAndBootstrapFlag()
        {
            try
            {
                if (Application.isPlaying)
                {
                    // 失敗時の後始末でも終了時の既知の通信ログを吸収する
                    // Ignore known shutdown connection logs during failure cleanup as well
                    LogAssert.ignoreFailingMessages = true;
                    yield return new ExitPlayMode();
                }
            }
            finally
            {
                // PlayModeで変更したEditorの描画値も必ず戻す
                // Always restore the editor rendering value changed during PlayMode
                QualitySettings.lodBias = SessionState.GetFloat("GraphicsQualityEntryTest_LodBias", QualitySettings.lodBias);
                SessionState.EraseFloat("GraphicsQualityEntryTest_LodBias");
                SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            }
        }

        [UnityTest]
        public IEnumerator InitialPreferenceAndChangesReachLateObjectsIncludingLandmarks()
        {
            SessionState.SetFloat("GraphicsQualityEntryTest_LodBias", QualitySettings.lodBias);
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;
            yield return Body().ToCoroutine();
            // TearDownでログ許容がリセットされる前に通信を終了する
            // Shut down the connection before the test framework resets log handling for TearDown
            yield return new ExitPlayMode();

            #region Internal

            async UniTask Body()
            {
                var hadSaved = PlayerPrefs.HasKey(GraphicsQualityPreference.PreferenceKey);
                var saved = PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey);
                GameObject prefab = null;
                MapObjectDistanceVisibilityController fixtureDistance = null;
                try
                {
                    // 起動前の低をConstructの初期適用で受け取る
                    // Receive the pre-boot low preference through Construct's initial application
                    GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
                    await LoadMainGame();
                    prefab = new GameObject("quality map object fixture");
                    prefab.AddComponent<MapObjectGameObject>();
                    prefab.AddComponent<MeshRenderer>();
                    var store = Object.FindFirstObjectByType<MapObjectGameObjectDatastore>(FindObjectsInactive.Include);
                    await store.IsNearFieldInstantiated.Where(static ready => ready).First().ToUniTask();
                    var runner = GetField(store, "_instantiationRunner");
                    var original = (MapObjectLayoutInstantiator)GetField(runner, "_instantiator");
                    var visibilityRegistry = (MapObjectRendererVisibilityRegistry)GetField(original, "_visibilityRegistry");
                    // 固定layout満杯の距離枠へ試験個体を追加しない
                    // Keep fixture objects out of the distance capacity reserved for the fixed layout
                    fixtureDistance = new MapObjectDistanceVisibilityController(2, System.Threading.CancellationToken.None);
                    var instantiator = new MapObjectLayoutInstantiator((Transform)GetField(original, "_parent"),
                        (MapObjectRegistry)GetField(original, "_registry"),
                        (Dictionary<int, GetMapObjectInfoProtocol.MapObjectsInfoMessagePack>)GetField(original, "_snapshotByInstanceId"),
                        fixtureDistance, visibilityRegistry);
                    Assert.IsFalse((bool)GetField(visibilityRegistry, "_isQualityVisible"));
                    var lateRenderers = new List<Renderer>();
                    var snapshots = (Dictionary<int, GetMapObjectInfoProtocol.MapObjectsInfoMessagePack>)GetField(instantiator, "_snapshotByInstanceId");
                    var nextId = int.MinValue;
                    var landmarkCount = 0;
                    var ordinaryCount = 0;

                    // 通常個体とランドマークを実際の生成入口へ後着させる
                    // Deliver ordinary objects and landmarks late through the actual instantiation entry
                    foreach (var master in MasterHolder.MapObjectMaster.Map.MapObjects)
                    {
                        var landmark = master.DistanceVisibilityType == MapObjectMasterElement.DistanceVisibilityTypeConst.landmark;
                        if (landmark ? landmarkCount != 0 : ordinaryCount != 0) continue;
                        while (snapshots.ContainsKey(nextId)) nextId++;
                        snapshots.Add(nextId, new GetMapObjectInfoProtocol.MapObjectsInfoMessagePack(nextId, false, 100));
                        var position = CameraManager.MainCamera.Camera.transform.position;
                        var layout = new MapObjectLayoutMessagePack(nextId, master.MapObjectGuid.ToString(), position.x, position.y, position.z,
                            1, 1, 1, 0, 0, 0, 1);
                        Assert.IsTrue(instantiator.InstantiateFromLayout(layout, prefab));
                        foreach (var instance in Object.FindObjectsByType<MapObjectGameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        {
                            if (instance.InstanceId != nextId) continue;
                            lateRenderers.Add(instance.GetComponent<MeshRenderer>());
                        }
                        nextId++;
                        if (landmark) landmarkCount++; else ordinaryCount++;
                    }
                    Assert.AreEqual(1, landmarkCount, "the test master must contain a landmark");
                    Assert.AreEqual(1, ordinaryCount);
                    Assert.AreEqual(2, lateRenderers.Count);
                    foreach (var renderer in lateRenderers) Assert.IsFalse(renderer.enabled);
                    GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.High);
                    foreach (var renderer in lateRenderers) Assert.IsTrue(renderer.enabled);
                    GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
                    foreach (var renderer in lateRenderers) Assert.IsFalse(renderer.enabled);
                }
                finally
                {
                    // 端末の保存値をテスト前へ戻す
                    // Restore the device preference to its pre-test value
                    if (hadSaved) PlayerPrefs.SetString(GraphicsQualityPreference.PreferenceKey, saved);
                    else PlayerPrefs.DeleteKey(GraphicsQualityPreference.PreferenceKey);
                    PlayerPrefs.Save();
                    fixtureDistance?.Shutdown();
                    Object.Destroy(prefab);
                }
            }

            object GetField(object target, string name)
            {
                return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
            }

            #endregion
        }
    }
}
