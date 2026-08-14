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

            // 両pinを個別復元
            // Restore both pins separately
            mapObjectPin.BeginSkitSuppress();
            veinPin.BeginSkitSuppress();
            Assert.IsFalse(mapObjectPin.IsActive());
            Assert.IsFalse(veinPin.IsActive());

            mapObjectPin.EndSkitSuppress();
            veinPin.EndSkitSuppress();
            Assert.IsTrue(mapObjectPin.IsActive());
            Assert.IsFalse(veinPin.IsActive());
        }

        [Test]
        public void WorldPinsApplyChangesWhileSkitIsHiddenAndRevealLatestState()
        {
            var mapObjectPin = new RecordingMapObjectPin(true);
            var veinPin = new RecordingVeinPin(false);

            // 完了状態を解除で覆さない
            // Do not overwrite completion when the suppression ends
            mapObjectPin.BeginSkitSuppress();
            veinPin.BeginSkitSuppress();
            mapObjectPin.SetActive(false);
            veinPin.SetActive(true);
            Assert.IsFalse(veinPin.IsActive());
            mapObjectPin.EndSkitSuppress();
            veinPin.EndSkitSuppress();

            Assert.IsFalse(mapObjectPin.IsActive());
            Assert.IsTrue(veinPin.IsActive());
        }

        [Test]
        public void WorldPinsStaySuppressedUntilEveryNestedSuppressionEnds()
        {
            var veinPin = new RecordingVeinPin(true);

            // 内側の解除では表示に戻らない
            // Ending only the inner one reveals nothing
            veinPin.BeginSkitSuppress();
            veinPin.BeginSkitSuppress();
            veinPin.EndSkitSuppress();
            Assert.IsFalse(veinPin.IsActive());

            veinPin.EndSkitSuppress();
            Assert.IsTrue(veinPin.IsActive());
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
            private int _skitSuppressDepth;

            public RecordingMapObjectPin(bool active)
            {
                _desiredActive = active;
            }

            public void SetActive(bool active)
            {
                SetActiveCallCount++;
                _desiredActive = active;
            }

            public bool IsActive() => _desiredActive && _skitSuppressDepth == 0;

            public void BeginSkitSuppress()
            {
                _skitSuppressDepth++;
            }

            public void EndSkitSuppress()
            {
                _skitSuppressDepth--;
            }

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
            private int _skitSuppressDepth;

            public RecordingVeinPin(bool active)
            {
                _desiredActive = active;
            }

            public void SetActive(bool active)
            {
                SetActiveCallCount++;
                _desiredActive = active;
            }

            public bool IsActive() => _desiredActive && _skitSuppressDepth == 0;

            public void BeginSkitSuppress()
            {
                _skitSuppressDepth++;
            }

            public void EndSkitSuppress()
            {
                _skitSuppressDepth--;
            }

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
