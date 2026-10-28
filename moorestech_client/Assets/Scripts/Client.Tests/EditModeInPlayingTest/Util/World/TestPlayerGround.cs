using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.Player;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.EditModeInPlayingTest.Util.World
{
    // 地形を持たないテストワールドに、実プレイヤー用の足場を用意する
    // Supplies real ground for the player in the terrain-free test world
    internal static class TestPlayerGround
    {
        private const float GroundedTimeoutSeconds = 5f;

        public static GameObject Create()
        {
            var position = PlayerSystemContainer.Instance.PlayerObjectController.Position;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "TestPlayerGround";
            ground.layer = LayerConst.GroundLayer;

            // 足元を覆い重力は通常どおり
            // Cover the feet; gravity stays normal
            ground.transform.position = new Vector3(position.x, Mathf.Floor(position.y) - 0.5f, position.z);
            ground.transform.localScale = new Vector3(12f, 1f, 12f);
            Physics.SyncTransforms();
            return ground;
        }

        public static async UniTask AssertGroundedAsync()
        {
            var player = (PlayerObjectController)PlayerSystemContainer.Instance.PlayerObjectController;
            var character = player.GetComponent<CharacterController>();

            // 地表探査と接地を確認
            // Verify the ground probe and contact
            Assert.IsTrue(GroundHeightProbe.TryGetGroundPoint(player.Position.x, player.Position.z, out _));

            // 足場を置く前の落下ぶんを落ち切るまで待つ。フレームの速さは環境で桁違いなので、回数でなく実時間で区切る
            // Wait until the fall begun before the ground existed completes; frame rate varies by orders across environments, so bound by real time, not frame count
            var startPosition = player.Position;
            var deadline = Time.realtimeSinceStartup + GroundedTimeoutSeconds;
            var frames = 0;
            while (!character.isGrounded && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.NextFrame();
                frames++;
            }
            Assert.IsTrue(character.isGrounded, $"足場へ接地しない player:{player.Position} start:{startPosition} frames:{frames}");
            Debug.Log($"[TestPlayerGround] grounded player:{player.Position}");
        }
    }
}
