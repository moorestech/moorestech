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

        // 落下復帰の閾値(-50)より上で、地表探査の届く範囲の内側
        // Above the fall-recovery threshold (-50) and inside the ground probe range
        private const float MinGroundTopY = -40f;

        public static GameObject Create()
        {
            var playerController = PlayerSystemContainer.Instance.PlayerObjectController;
            var position = playerController.Position;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "TestPlayerGround";
            ground.layer = LayerConst.GroundLayer;

            // 落下復帰の繰り返しで落下速度が積もり探査範囲の外まで落ちうるため、足場の高さに下限を設ける
            // Repeated fall recovery accumulates fall speed and can drop the player below the probe range, so clamp the ground height
            var groundTopY = Mathf.Max(Mathf.Floor(position.y), MinGroundTopY);
            ground.transform.position = new Vector3(position.x, groundTopY - 0.5f, position.z);
            ground.transform.localScale = new Vector3(12f, 1f, 12f);

            // 足場より下にいるときだけ足場の上へ戻す。足と足場が重なると接地しないため少し上に置く
            // Lift the player onto the ground only when below it; place slightly above since overlapping feet ignore the ground
            if (position.y < groundTopY) playerController.SetPlayerPosition(new Vector3(position.x, groundTopY + 0.1f, position.z));
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
