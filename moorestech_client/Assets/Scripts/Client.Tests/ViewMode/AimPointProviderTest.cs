using Client.Game.InGame.Control.ViewMode;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.ViewMode
{
    /// <summary>
    ///     モード別照準座標を検証するテスト
    ///     Tests verifying AimPointProvider aim points per view mode
    /// </summary>
    public class AimPointProviderTest
    {
        [TearDown]
        public void TearDown()
        {
            AimPointProvider.SetMode(PlayerViewMode.ThirdPerson);
        }

        [Test]
        public void FirstPersonReturnsScreenCenter()
        {
            AimPointProvider.SetMode(PlayerViewMode.FirstPerson);
            var point = AimPointProvider.GetAimScreenPoint();
            Assert.AreEqual(Screen.width / 2f, point.x);
            Assert.AreEqual(Screen.height / 2f, point.y);
        }

        [Test]
        public void SetModeUpdatesCurrentMode()
        {
            AimPointProvider.SetMode(PlayerViewMode.FirstPerson);
            Assert.AreEqual(PlayerViewMode.FirstPerson, AimPointProvider.CurrentMode);
        }
    }
}
