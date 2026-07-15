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
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
        }

        [Test]
        public void ScreenCenterModeReturnsScreenCenter()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            var point = AimPointProvider.GetAimScreenPoint();
            Assert.AreEqual(Screen.width / 2f, point.x);
            Assert.AreEqual(Screen.height / 2f, point.y);
        }

        [Test]
        public void FirstPersonUsesScreenCenterAim()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());
        }

        [Test]
        public void ThirdPersonUsesMouseAim()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.GetCurrentMode());
        }
    }
}
