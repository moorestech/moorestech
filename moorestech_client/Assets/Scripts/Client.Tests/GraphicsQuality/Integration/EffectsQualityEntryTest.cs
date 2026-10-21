using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.GraphicsQuality.Appliers;
using Client.Game.InGame.Train.View.Object.Core;
using Game.Train.Unit;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.GraphicsQuality
{
    [Category("CiShardClientPlay3")]
    public class EffectsQualityEntryTest
    {
        [UnityTearDown]
        public IEnumerator RestorePlayModeAndBootstrapFlag()
        {
            try
            {
                if (Application.isPlaying) yield return new ExitPlayMode();
            }
            finally { UnityEditor.SessionState.SetBool("DebugObjectsBootstrap_Disabled", false); }
        }

        [UnityTest]
        public IEnumerator StartAndCreationAndPlacementEventsApplyToBlocksAndCarsAndReleaseDestroyedSwitches()
        {
            Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil.EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return Body();

            #region Internal

            static IEnumerator Body()
            {
                var owned = new List<Object>();
                var hadSaved = PlayerPrefs.HasKey(GraphicsQualityPreference.PreferenceKey);
                var saved = PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey);
                var subject = (Subject<GraphicsQualitySettings>)GraphicsQualityPreference.OnSettingsChanged;
                var observerField = typeof(Subject<GraphicsQualitySettings>).GetField("outObserver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var observer = observerField.GetValue(subject);
                try
                {
                    var store = Own(new GameObject("blocks")).AddComponent<BlockGameObjectDataStore>();
                    var trains = Own(new GameObject("trains")).AddComponent<TrainCarObjectDatastore>();
                    var existingBlock = CreateBlock("existing block", null);
                    var existingCar = CreateCar("existing car", null);
                    ((Dictionary<Vector3Int, BlockGameObject>)GetField(store, "_blockObjectsDictionary")).Add(Vector3Int.zero, existingBlock);
                    ((Dictionary<TrainCarInstanceId, TrainCarEntityObject>)GetField(trains, "_entities")).Add(new TrainCarInstanceId(1), existingCar);
                    var applier = new EffectsQualityApplier(store, trains);
                    GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
                    applier.Start();
                    Assert.IsFalse(existingBlock.GetComponent<Light>().enabled);
                    Assert.IsFalse(existingCar.GetComponent<Light>().enabled);

                    // 生成・設置完了通知から再停止を検証
                    // Verify new instances and stopping again through creation and placement-completion notifications
                    var placed = CreateBlock("placed block", null);
                    var newCar = CreateCar("new car", null);
                    var particleRoot = Own(new GameObject("placement particle"));
                    particleRoot.transform.SetParent(placed.transform, false);
                    particleRoot.SetActive(false);
                    var particle = particleRoot.AddComponent<ParticleSystem>();
                    Assert.IsFalse(particle.isPlaying);
                    ((Subject<BlockGameObject>)GetField(store, "_onBlockPlaced")).OnNext(placed);
                    ((Subject<TrainCarEntityObject>)GetField(trains, "_onTrainCarCreated")).OnNext(newCar);
                    Assert.IsFalse(placed.GetComponent<Light>().enabled);
                    Assert.IsFalse(newCar.GetComponent<Light>().enabled);
                    // 実際の非active→再有効化でplayOnAwakeが再生する経路を通す
                    // Exercise actual inactive-to-active playOnAwake playback
                    particleRoot.SetActive(true);
                    yield return null;
                    Assert.IsTrue(particle.isPlaying);
                    placed.GetComponent<Light>().enabled = true;
                    ((Subject<BlockGameObject>)GetField(placed, "_onFinishedPlaceAnimation")).OnNext(placed);
                    Assert.IsFalse(placed.GetComponent<Light>().enabled);
                    Assert.IsFalse(particle.isPlaying);
                    GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.High);
                    foreach (var root in new[] { existingBlock.gameObject, existingCar.gameObject, placed.gameObject, newCar.gameObject })
                        Assert.IsTrue(root.GetComponent<Light>().enabled);

                    Assert.IsTrue(particle.isPlaying);

                    // 設定変更なしでも破棄で保持解除
                    // Destruction releases references without a setting change
                    var switches = (HashSet<EffectsQualitySwitch>)GetField(applier, "_switches");
                    Assert.AreEqual(4, switches.Count);
                    Object.DestroyImmediate(placed.gameObject);
                    for (var frame = 0; frame < 10 && switches.Count != 3; frame++) yield return null;
                    Assert.AreEqual(3, switches.Count);

                    // 未有効化個体も数フレーム後に保持解除
                    // Release never-activated instances after destruction and frame advancement
                    var inactiveParent = Own(new GameObject("inactive parent"));
                    inactiveParent.SetActive(false);
                    var inactiveBlock = CreateBlock("inactive block", inactiveParent.transform);
                    var inactiveCar = CreateCar("inactive car", inactiveParent.transform);
                    ((Subject<BlockGameObject>)GetField(store, "_onBlockPlaced")).OnNext(inactiveBlock);
                    ((Subject<TrainCarEntityObject>)GetField(trains, "_onTrainCarCreated")).OnNext(inactiveCar);
                    Assert.AreEqual(5, switches.Count);
                    Object.DestroyImmediate(inactiveBlock.gameObject);
                    Object.DestroyImmediate(inactiveCar.gameObject);
                    // 監視による解除前でも生存個体へ配信する
                    // Deliver to survivors even before the destruction monitor releases references
                    Assert.DoesNotThrow(() => GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low));
                    foreach (var root in new[] { existingBlock.gameObject, existingCar.gameObject, newCar.gameObject })
                        Assert.IsFalse(root.GetComponent<Light>().enabled);
                    for (var frame = 0; frame < 10 && switches.Count != 3; frame++)
                    {
                        yield return null;
                        // batchmodeでは描画終端が来ないためUniRxの実監視を進める
                        // Advance the actual UniRx monitor because batchmode does not execute WaitForEndOfFrame
                        if (Application.isBatchMode)
                        {
                            var dispatcher = Object.FindFirstObjectByType<MainThreadDispatcher>();
                            ((UniRx.InternalUtil.MicroCoroutine)GetField(dispatcher, "endOfFrameMicroCoroutine")).Run();
                        }
                    }
                    Assert.AreEqual(3, switches.Count);

                    // 解除後の設定通知が残存個体にだけ届く
                    // Setting changes after release reach only surviving instances
                    GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
                    foreach (var root in new[] { existingBlock.gameObject, existingCar.gameObject, newCar.gameObject })
                        Assert.IsFalse(root.GetComponent<Light>().enabled);
                }
                finally
                {
                    // 通知先と端末設定を破棄前に復元する
                    // Restore observers and device preferences before destroying fixtures
                    observerField.SetValue(subject, observer);
                    if (hadSaved) PlayerPrefs.SetString(GraphicsQualityPreference.PreferenceKey, saved);
                    else PlayerPrefs.DeleteKey(GraphicsQualityPreference.PreferenceKey);
                    PlayerPrefs.Save();
                    for (var index = owned.Count - 1; index >= 0; index--) Object.DestroyImmediate(owned[index]);
                }
                #region Internal

                T Own<T>(T value) where T : Object
                {
                    owned.Add(value);
                    return value;
                }

                object GetField(object target, string name)
                {
                    return target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(target);
                }


                BlockGameObject CreateBlock(string name, Transform parent)
                {
                    var root = Own(new GameObject(name));
                    root.transform.SetParent(parent, false);
                    root.AddComponent<Light>();
                    return root.AddComponent<BlockGameObject>();
                }

                TrainCarEntityObject CreateCar(string name, Transform parent)
                {
                    var root = Own(new GameObject(name));
                    root.transform.SetParent(parent, false);
                    root.AddComponent<Light>();
                    return root.AddComponent<TrainCarEntityObject>();
                }

                #endregion
            }

            #endregion
        }
    }
}
