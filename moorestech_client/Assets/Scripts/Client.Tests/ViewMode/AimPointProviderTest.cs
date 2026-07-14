using Client.Game.InGame.Control.ViewMode;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.ViewMode
{
    public class AimPointProviderTest
    {
        [TearDown]
        public void TearDown()
        {
            AimPointProvider.SetMode(AimPointMode.Mouse);
        }

        [Test]
        public void ScreenCenterModeReturnsScreenCenter()
        {
            AimPointProvider.SetMode(AimPointMode.ScreenCenter);
            var point = AimPointProvider.GetAimScreenPoint();
            Assert.AreEqual(Screen.width / 2f, point.x);
            Assert.AreEqual(Screen.height / 2f, point.y);
        }

        [Test]
        public void SetModeUpdatesCurrentMode()
        {
            AimPointProvider.SetMode(AimPointMode.ScreenCenter);
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.CurrentMode);
        }
    }
}
