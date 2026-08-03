using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Player;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;

namespace Client.Tests.Player
{
    public class PlayerRideFollowTest
    {
        private readonly List<GameObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _createdObjects)
            {
                if (created != null) Object.DestroyImmediate(created);
            }
            _createdObjects.Clear();
        }

        [Test]
        public void 乗車追従でプレイヤーのposeが車両に一致する()
        {
            var controller = CreateStartedPlayerObjectController(out _);

            // 車両をyaw90で置く。ローカル(0,1,2)は世界の(2,1,0)へ回るので、車両位置の素通しでは出ない値になる
            // Place the car at yaw 90 so local (0,1,2) becomes world (2,1,0), a value a plain car-position copy cannot produce
            var trainCar = CreateTrainCarTransform(new Vector3(10f, 5f, 20f), Quaternion.Euler(0f, 90f, 0f));
            controller.SetRideFollowTarget(trainCar, new Vector3(0f, 1f, 2f), Quaternion.Euler(0f, 30f, 0f));

            InvokeLateUpdate(controller);

            Assert.AreEqual(12f, controller.Position.x, 0.001f, "車両のローカル基準でXが追従していない");
            Assert.AreEqual(6f, controller.Position.y, 0.001f, "車両のローカル基準でYが追従していない");
            Assert.AreEqual(20f, controller.Position.z, 0.001f, "車両のローカル基準でZが追従していない");

            // 車両yaw90とローカルyaw30の合成
            // Composition of the car yaw 90 and the local yaw 30
            var expectedRotation = Quaternion.Euler(0f, 120f, 0f);
            Assert.AreEqual(0f, Quaternion.Angle(expectedRotation, controller.transform.rotation), 0.01f, "車両の向きへ追従していない");
        }

        [Test]
        public void 降車でThirdPersonControllerの有効状態が乗車前へ戻る()
        {
            // 乗車前に動けたなら降車後も動けること。戻し損ねるとプレイヤーが永久に操作不能になる
            // A player who could move before riding must move after dismounting; failing to restore freezes them forever
            var playablePlayer = CreateStartedPlayerObjectController(out var playableThirdPersonController);
            Assert.IsTrue(playableThirdPersonController.enabled, "前提が崩れている。開始後は有効なはず");

            playablePlayer.SetRideFollowTarget(CreateTrainCarTransform(Vector3.zero, Quaternion.identity), Vector3.zero, Quaternion.identity);
            Assert.IsFalse(playableThirdPersonController.enabled, "乗車中に重力・Move・足場追従が止まっていない");

            playablePlayer.ClearRideFollowTarget();
            Assert.IsTrue(playableThirdPersonController.enabled, "降車後も操作不能のまま");

            // UIロック中は元から無効。降車で無条件に有効化するとロックが破れる
            // A UI lock leaves it disabled beforehand; unconditionally enabling it on dismount would break the lock
            var lockedPlayer = CreateStartedPlayerObjectController(out var lockedThirdPersonController);
            lockedThirdPersonController.enabled = false;

            lockedPlayer.SetRideFollowTarget(CreateTrainCarTransform(Vector3.zero, Quaternion.identity), Vector3.zero, Quaternion.identity);
            lockedPlayer.ClearRideFollowTarget();
            Assert.IsFalse(lockedThirdPersonController.enabled, "元々無効だったのに降車で有効化された");
        }

        private PlayerObjectController CreateStartedPlayerObjectController(out ThirdPersonController thirdPersonController)
        {
            var playerRoot = new GameObject("PlayerRideFollowTestPlayer");
            _createdObjects.Add(playerRoot);
            playerRoot.AddComponent<CharacterController>();
            playerRoot.AddComponent<StarterAssetsInputs>();
            thirdPersonController = playerRoot.AddComponent<ThirdPersonController>();

            var playerObjectController = playerRoot.AddComponent<PlayerObjectController>();
            SetField(playerObjectController, "controller", thirdPersonController);

            // 乗車追従はStartPlayerRuntime後のLateUpdateで動くため、実行開始まで進めておく
            // Riding follow runs in LateUpdate after StartPlayerRuntime, so advance the player to the started state
            playerObjectController.Initialize(Vector3.zero, Vector3.zero);
            playerObjectController.StartPlayerRuntime();
            return playerObjectController;
        }

        private Transform CreateTrainCarTransform(Vector3 position, Quaternion rotation)
        {
            var trainCar = new GameObject("PlayerRideFollowTestTrainCar");
            _createdObjects.Add(trainCar);
            trainCar.transform.SetPositionAndRotation(position, rotation);
            return trainCar.transform;
        }

        private static void InvokeLateUpdate(PlayerObjectController controller)
        {
            var method = typeof(PlayerObjectController).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
