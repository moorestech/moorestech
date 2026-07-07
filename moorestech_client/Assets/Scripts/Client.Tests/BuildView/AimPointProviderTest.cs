using Client.Game.InGame.Control.BuildView;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BuildView
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
            AimPointProvider.SetMode(BuildViewMode.TopDown);
        }

        [Test]
        public void FirstPersonReturnsScreenCenter()
        {
            AimPointProvider.SetMode(BuildViewMode.FirstPerson);
            var point = AimPointProvider.GetAimScreenPoint();
            Assert.AreEqual(Screen.width / 2f, point.x);
            Assert.AreEqual(Screen.height / 2f, point.y);
        }

        [Test]
        public void SetModeUpdatesCurrentMode()
        {
            AimPointProvider.SetMode(BuildViewMode.FirstPerson);
            Assert.AreEqual(BuildViewMode.FirstPerson, AimPointProvider.CurrentMode);
        }
    }
}
