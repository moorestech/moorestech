using System.Collections;
using Client.Game.InGame.Player;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Client.Tests.EditModeInPlayingTest.OsInput;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// This test is executed in EditMode, but it switches to PlayMode during execution.
    /// </summary>
    [Category("IgnoreCI")]
    public class PlayerMovementTest
    {
        /// <summary>
        /// CI/バッチ環境で仮想デバイスを確保する
        /// Ensure virtual input devices exist in CI/batch environments.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            OsInputSpoof.EnsureDevices();
        }

        /// <summary>
        /// テスト終了時にキー状態リークを防止し仮想デバイスを破棄する
        /// Prevent key state leaks and clean up virtual devices on test end.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            OsInputSpoof.ReleaseAllKeys();
            OsInputSpoof.CleanupDevices();
        }

        [UnityTest]
        public IEnumerator WAndDKeys_MovePlayerForwardRightTest()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return TestBody().ToCoroutine();

            yield return new ExitPlayMode();

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                // OS 入力注入が使用不可なら Assert.Ignore でスキップ
                // Skip via Assert.Ignore if OS input injection is not available
                OsInputSpoof.AssertAvailableOrSkip();

                // ゲーム起動
                // Start the game
                await LoadMainGame();

                // プレイヤーオブジェクトの取得
                // Get the player object
                var playerController = PlayerSystemContainer.Instance.PlayerObjectController;
                Assert.IsNotNull(playerController, "PlayerObjectController was not found.");

                // カメラの取得（移動方向の基準となる）
                // Get the camera (used as reference for movement direction)
                var mainCamera = Camera.main;
                Assert.IsNotNull(mainCamera, "Main camera was not found.");

                // 安定のため数フレーム待機
                // Wait several frames for stability
                await UniTask.DelayFrame(30);

                // 移動前の位置を記録
                // Record position before movement
                var positionBefore = playerController.Position;

                // カメラの前方・右方向をXZ平面に射影（Y成分は無視）
                // Project camera forward/right onto XZ plane (ignore Y component)
                var cameraForwardXZ = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
                var cameraRightXZ = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;

                // W+Dキーを注入してプレイヤーを右前方向に移動
                // Inject W+D keys to move player forward-right
                OsInputSpoof.KeyDown(OsInputSpoof.DebugKey.W);
                OsInputSpoof.KeyDown(OsInputSpoof.DebugKey.D);

                // 2秒間キーを押し続ける（移動に十分な時間）
                // Hold keys for 2 seconds (enough time for movement)
                await UniTask.Delay(2000);

                // キーを離す
                // Release keys
                OsInputSpoof.KeyUp(OsInputSpoof.DebugKey.W);
                OsInputSpoof.KeyUp(OsInputSpoof.DebugKey.D);

                // 移動後の位置を取得
                // Get position after movement
                var positionAfter = playerController.Position;

                // 移動量をXZ平面上で算出
                // Calculate displacement on XZ plane
                var displacement = positionAfter - positionBefore;
                var displacementXZ = new Vector3(displacement.x, 0f, displacement.z);

                // プレイヤーが実際に移動したことを確認
                // Verify the player actually moved
                Assert.Greater(displacementXZ.magnitude, 0.5f,
                    $"Player did not move enough. Displacement={displacementXZ}, magnitude={displacementXZ.magnitude}");

                // カメラ前方・右方向への射影でそれぞれ正の移動量があることを確認
                // Verify positive displacement along both camera-forward and camera-right
                var forwardComponent = Vector3.Dot(displacementXZ, cameraForwardXZ);
                var rightComponent = Vector3.Dot(displacementXZ, cameraRightXZ);

                Assert.Greater(forwardComponent, 0f,
                    $"Player should move forward (camera-relative). Forward component={forwardComponent}, displacement={displacementXZ}");
                Assert.Greater(rightComponent, 0f,
                    $"Player should move right (camera-relative). Right component={rightComponent}, displacement={displacementXZ}");
            }

            #endregion
        }
    }
}
