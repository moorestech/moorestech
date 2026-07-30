using System.Reflection;
using System.Text.RegularExpressions;
using Client.Common;
using Client.Game.InGame.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Player
{
    public class PlayerFallRecoveryPositionTest
    {
        private GameObject _ground;

        [TearDown]
        public void TearDown()
        {
            if (_ground != null) Object.DestroyImmediate(_ground);
        }

        [Test]
        public void ResolveFallRecoveryPosition_Spawnの実地表Yへ復帰する()
        {
            // LayoutのSpawn Yと異なる地表を作り、template実機で観測した地中復帰を再現する
            // Build ground above Layout Spawn Y to reproduce the underground template recovery seen in the Player
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "PlayerFallRecoveryGround";
            _ground.layer = LayerConst.GroundLayer;
            _ground.transform.position = new Vector3(5f, 65f, 5f);
            _ground.transform.localScale = new Vector3(4f, 1f, 4f);
            Physics.SyncTransforms();

            // 地形外の現在地を失敗させた後、Spawn XZ直下の地表を復帰先に採用する
            // Fail the off-terrain current position, then select ground directly below the Spawn XZ
            var method = typeof(PlayerObjectController).GetMethod(
                "ResolveFallRecoveryPosition", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            LogAssert.Expect(LogType.Error, new Regex("^地面が見つかりませんでした"));
            var result = (Vector3)method.Invoke(null, new object[]
            {
                new Vector3(100000f, -1000f, 100000f),
                new Vector3(5f, 15f, 5f),
            });

            Assert.That(result.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(result.y, Is.EqualTo(65.5f).Within(0.001f));
            Assert.That(result.z, Is.EqualTo(5f).Within(0.001f));
        }
    }
}
