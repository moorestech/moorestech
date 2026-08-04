using System;
using System.Reflection;
using System.Threading.Tasks;
using Client.Game.InGame.Tutorial;
using Client.Game.Skit;
using Client.Game.Skit.Localization;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.ChallengesModule;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Localization.Skit
{
    public class SkitFailureCleanupTest
    {
        private const string SkitKey = "skit.opening.7.body";

        [Test]
        public void SkitFailureRestoresPlaybackStateAndLeavesUntouchedMapPinAlone()
        {
            var root = new GameObject("SkitManagerTest");
            root.SetActive(false);
            var skitUiObject = new GameObject("SkitUI");
            skitUiObject.transform.SetParent(root.transform);
            var skitUi = skitUiObject.AddComponent<SkitUI>();
            var manager = root.AddComponent<SkitManager>();
            var mapObjectPin = new RecordingMapObjectPin(false);
            var veinPin = new RecordingVeinPin(false);
            SetPrivateField(manager, "skitUI", skitUi);
            SetPrivateField(manager, "mapObjectPin", mapObjectPin);
            SetPrivateField(manager, "veinPin", veinPin);

            // 失敗したskitはfinallyだけで後始末し、まだ隠していないpinには触れない
            // A failed skit cleans up solely in finally and never touches a pin it has not hidden
            var startSkit = typeof(SkitManager).GetMethod(
                "StartSkit",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(TextAsset) },
                null);
            var invalidTitleAsset = new TextAsset("[]");
            var startTask = (UniTask)startSkit.Invoke(manager, new object[] { invalidTitleAsset });

            Assert.ThrowsAsync<ArgumentException>(async () => await startTask);
            Assert.IsFalse(manager.IsPlayingSkit);
            Assert.IsFalse(skitUiObject.activeSelf);
            Assert.AreEqual(0, mapObjectPin.SetActiveCallCount);
            Assert.AreEqual(0, veinPin.SetActiveCallCount);

            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public async Task DisposedResolverStopsReloadingAndToleratesRepeatedDispose()
        {
            var loader = new FakeSkitDictionaryLoader();
            loader.Set("japanese", SkitKey, "Japanese");
            loader.Set("english", SkitKey, "English");
            loader.Set("french", SkitKey, "French");
            var source = new FakeSkitLocalizationSource();
            var resolver = new SkitLocalizationResolver(loader, source);
            await resolver.PrepareAsync("opening");

            // cleanupが二重に走ってもDisposeは安全で、破棄後は言語変更を追わない
            // A repeated cleanup keeps Dispose safe, and a disposed resolver stops following language changes
            resolver.Dispose();
            resolver.Dispose();
            source.SetLanguage("french");
            await UniTask.DelayFrame(5);

            Assert.AreEqual(0, loader.GetLoadCount("french"));
            Assert.AreEqual("Japanese", resolver.ResolveCommandField(7, "body", "Source"));
        }

        [Test]
        public void WorldPinsRestoreTheirIndividualPreSkitActiveStates()
        {
            var mapObjectPin = new RecordingMapObjectPin(true);
            var veinPin = new RecordingVeinPin(false);
            var snapshot = new WorldPinActivationSnapshot(mapObjectPin, veinPin);

            // skit中は両方を隠し、終了時は各pinの開始前状態へ個別に戻す
            // Hide both pins during skit, then restore each pin's own pre-skit state
            snapshot.Hide();
            Assert.IsFalse(mapObjectPin.IsActiveSelf());
            Assert.IsFalse(veinPin.IsActiveSelf());

            snapshot.Restore();
            Assert.IsTrue(mapObjectPin.IsActiveSelf());
            Assert.IsFalse(veinPin.IsActiveSelf());
        }

        [Test]
        public void WorldPinsApplyChangesWhileSkitIsHiddenAndRevealLatestState()
        {
            var mapObjectPin = new RecordingMapObjectPin(true);
            var veinPin = new RecordingVeinPin(false);
            var snapshot = new WorldPinActivationSnapshot(mapObjectPin, veinPin);

            // 非表示中のチュートリアル完了を、開始前状態の復元で上書きしない
            // Do not overwrite tutorial completion while hidden with the pre-skit state
            snapshot.Hide();
            mapObjectPin.SetActive(false);
            veinPin.SetActive(true);
            Assert.IsFalse(veinPin.IsActiveSelf());
            snapshot.Restore();

            Assert.IsFalse(mapObjectPin.IsActiveSelf());
            Assert.IsTrue(veinPin.IsActiveSelf());
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            typeof(SkitManager)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private sealed class RecordingMapObjectPin : IMapObjectPin
        {
            public int SetActiveCallCount;
            private bool _desiredActive;
            private bool _skitSuppressed;

            public RecordingMapObjectPin(bool active)
            {
                _desiredActive = active;
            }

            public void SetActive(bool active)
            {
                SetActiveCallCount++;
                _desiredActive = active;
            }

            public bool IsActiveSelf() => _desiredActive && !_skitSuppressed;

            public void SetSkitSuppressed(bool suppressed)
            {
                _skitSuppressed = suppressed;
            }

            public bool IsSkitSuppressed() => _skitSuppressed;

            public ITutorialView ApplyTutorial(TutorialsElement tutorial)
            {
                return this;
            }

            public void CompleteTutorial()
            {
            }
        }

        private sealed class RecordingVeinPin : IVeinPin
        {
            public int SetActiveCallCount;
            private bool _desiredActive;
            private bool _skitSuppressed;

            public RecordingVeinPin(bool active)
            {
                _desiredActive = active;
            }

            public void SetActive(bool active)
            {
                SetActiveCallCount++;
                _desiredActive = active;
            }

            public bool IsActiveSelf() => _desiredActive && !_skitSuppressed;

            public void SetSkitSuppressed(bool suppressed)
            {
                _skitSuppressed = suppressed;
            }

            public bool IsSkitSuppressed() => _skitSuppressed;

            public ITutorialView ApplyTutorial(TutorialsElement tutorial)
            {
                return this;
            }

            public void CompleteTutorial()
            {
            }
        }
    }
}
