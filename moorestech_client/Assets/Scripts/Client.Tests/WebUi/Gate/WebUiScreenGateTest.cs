using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.WebUi.Gate
{
    public class WebUiScreenGateTest
    {
        [SetUp]
        public void SetUp()
        {
            WebUiScreenGate.SetWebUiMode(false);
            WebUiScreenGate.SetHostAvailable(false);
        }

        [TearDown]
        public void TearDown()
        {
            WebUiScreenGate.SetWebUiMode(false);
            WebUiScreenGate.SetHostAvailable(false);
        }

        [Test]
        public void EffectiveModeChangesArePublishedWithoutDuplicates()
        {
            var changes = new List<bool>();
            using var subscription = WebUiScreenGate.OnWebUiModeChanged.Subscribe(changes.Add);

            WebUiScreenGate.SetWebUiMode(true);
            WebUiScreenGate.SetHostAvailable(true);
            WebUiScreenGate.SetHostAvailable(true);
            WebUiScreenGate.SetWebUiMode(false);

            CollectionAssert.AreEqual(new[] { true, false }, changes);
        }

        [Test]
        public void HotBarVisibilityIsDeferredUntilStartInitialization()
        {
            // Start前は自身を止めず初期化を保つ
            // Preserve initialization by staying active before Start
            WebUiScreenGate.SetWebUiMode(true);
            WebUiScreenGate.SetHostAvailable(true);
            var hotBarObject = new GameObject("HotBarViewLifecycleTest");
            var hotBarView = hotBarObject.AddComponent<HotBarView>();

            hotBarView.SetActive(true);

            Assert.IsTrue(hotBarObject.activeSelf);
            Object.DestroyImmediate(hotBarObject);
        }
    }
}
