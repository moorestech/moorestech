using System.Reflection;
using Client.Common;
using Client.Game.InGame.Player;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;

namespace Client.Tests.Player
{
    public class PlayerRuntimeStartGateTest
    {
        private GameObject _playerRoot;
        private GameObject _ground;
        private ThirdPersonController _thirdPersonController;

        [TearDown]
        public void TearDown()
        {
            if (_playerRoot != null) Object.DestroyImmediate(_playerRoot);
            if (_ground != null) Object.DestroyImmediate(_ground);
        }

        [Test]
        public void StartPlayerRuntimeまでWarpも落下復帰も起きない()
        {
            var controller = CreatePlayerObjectController();

            // 地形構築前を模す。Initializeだけでは保存座標へ動かないこと
            // Emulate the pre-terrain window: Initialize alone must not move the player to the saved position
            controller.Initialize(new Vector3(30f, 70f, 40f), new Vector3(5f, 15f, 5f));
            Assert.That(controller.Position.x, Is.Not.EqualTo(30f).Within(0.001f), "Initializeの時点でWarpされている");

            // 重力が止まっていないと地形構築中に落下速度が終端まで蓄積し、Warp直後の1フレームで地形へめり込む
            // Leaving gravity on accumulates terminal fall speed during terrain build and sinks the player on the frame after the warp
            Assert.IsFalse(_thirdPersonController.enabled, "Initializeで重力が止まっていない");

            // 落下復帰が武装していないこと。武装していればGetGroundPointのLogErrorでテストが落ちる
            // Fall recovery must be disarmed; if armed, GetGroundPoint's LogError fails this test
            controller.transform.position = new Vector3(0f, -100f, 0f);
            InvokeLateUpdate(controller);
            Assert.AreEqual(-100f, controller.transform.position.y, 0.001f, "開始前に落下復帰が動いた");
        }

        [Test]
        public void StartPlayerRuntimeで保存座標へWarpし落下復帰が武装する()
        {
            var controller = CreatePlayerObjectController();
            controller.Initialize(new Vector3(30f, 70f, 40f), new Vector3(5f, 15f, 5f));

            controller.StartPlayerRuntime();

            // 重力を戻し損ねるとプレイヤーが永久に操作不能になる
            // Failing to restore gravity leaves the player permanently unable to move
            Assert.IsTrue(_thirdPersonController.enabled, "開始後も重力が止まったまま");

            Assert.AreEqual(30f, controller.Position.x, 0.001f, "保存座標Xへ復帰していない");
            Assert.AreEqual(70f, controller.Position.y, 0.001f, "保存座標Yへ復帰していない");
            Assert.AreEqual(40f, controller.Position.z, 0.001f, "保存座標Zへ復帰していない");

            // 開始後は落下復帰が効く。地表を用意し、そのYへ戻ることで武装を確かめる
            // After the start, fall recovery works; prepare ground and confirm the recovery Y
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.layer = LayerConst.GroundLayer;
            _ground.transform.position = new Vector3(30f, 20f, 40f);
            _ground.transform.localScale = new Vector3(4f, 1f, 4f);
            Physics.SyncTransforms();

            controller.transform.position = new Vector3(30f, -100f, 40f);
            InvokeLateUpdate(controller);
            Assert.AreEqual(20.5f, controller.transform.position.y, 0.001f, "開始後に落下復帰が動かない");
        }

        [Test]
        public void StartPlayerRuntimeの二重呼び出しは無視される()
        {
            var controller = CreatePlayerObjectController();
            controller.Initialize(new Vector3(30f, 70f, 40f), new Vector3(5f, 15f, 5f));
            controller.StartPlayerRuntime();

            controller.transform.position = new Vector3(1f, 2f, 3f);
            controller.StartPlayerRuntime();
            Assert.AreEqual(1f, controller.Position.x, 0.001f, "2回目の開始でWarpし直された");
        }

        private PlayerObjectController CreatePlayerObjectController()
        {
            _playerRoot = new GameObject("PlayerRuntimeStartGateTestPlayer");
            _playerRoot.AddComponent<CharacterController>();
            _playerRoot.AddComponent<StarterAssetsInputs>();
            _thirdPersonController = _playerRoot.AddComponent<ThirdPersonController>();
            var playerObjectController = _playerRoot.AddComponent<PlayerObjectController>();
            SetField(playerObjectController, "controller", _thirdPersonController);
            return playerObjectController;
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
