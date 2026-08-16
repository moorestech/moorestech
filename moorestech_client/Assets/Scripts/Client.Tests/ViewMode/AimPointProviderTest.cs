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
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.ScreenCenter);
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
            Assert.AreEqual(ThirdPersonAimSource.ScreenCenter, AimPointProvider.GetEffectiveAimSource());
        }

        [Test]
        public void FirstPersonIgnoresCursorAimSource()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.Cursor);
            Assert.AreEqual(ThirdPersonAimSource.ScreenCenter, AimPointProvider.GetEffectiveAimSource());
        }

        [Test]
        public void ThirdPersonWithScreenCenterSourceUsesScreenCenter()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.ScreenCenter);
            Assert.AreEqual(ThirdPersonAimSource.ScreenCenter, AimPointProvider.GetEffectiveAimSource());
        }

        [Test]
        public void ThirdPersonWithCursorSourceUsesMouseAim()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.Cursor);
            Assert.AreEqual(ThirdPersonAimSource.Cursor, AimPointProvider.GetEffectiveAimSource());
        }
    }
}
