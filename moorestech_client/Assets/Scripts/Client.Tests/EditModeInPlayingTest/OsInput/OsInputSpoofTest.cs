using System.Collections;
using Client.Tests.EditModeInPlayingTest.OsInput;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// OsInputSpoof による入力合成を EditMode で検証するテスト
    /// EditMode test that verifies input injection via OsInputSpoof.
    ///
    /// Editor (macOS/Windows) では InputSystem.QueueStateEvent を使用するため PlayMode 不要
    /// On Editor (macOS/Windows), uses InputSystem.QueueStateEvent so PlayMode is not required.
    ///
    /// 前提（macOS Standalone）: システム設定 → プライバシーとセキュリティ → アクセシビリティ で Unity を許可すること
    /// Prerequisite (macOS Standalone): Grant Unity permission in System Settings → Privacy & Security → Accessibility.
    ///
    /// 前提（Windows Standalone）: UIPI 制約により管理者権限が必要な場合がある
    /// Prerequisite (Windows Standalone): Administrator privileges may be required due to UIPI constraints.
    /// </summary>
    public class OsInputSpoofTest
    {
        /// <summary>
        /// バッチモード等でデバイスが存在しない場合に仮想デバイスを追加する
        /// Add virtual devices when not available (e.g. in CI headless/batch mode).
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            OsInputSpoof.EnsureDevices();
        }

        /// <summary>
        /// テストで使用する全キーを強制解放し、キー状態のリークを防止する
        /// Force-release all test keys to prevent key state leaks across tests.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            OsInputSpoof.ReleaseAllKeys();
            OsInputSpoof.CleanupDevices();
        }

        /// <summary>
        /// Space キーを注入し、Unity Input System が検知することを確認する
        /// Inject Space key and verify Unity Input System detects it.
        /// </summary>
        [UnityTest]
        public IEnumerator OsKeyInjection_SpaceKey_IsDetectedByInputSystem()
        {
            // OS 入力権限チェック（使用不可なら Assert.Ignore でスキップ）
            // Check OS input permission (skip with Assert.Ignore if unavailable)
            OsInputSpoof.AssertAvailableOrSkip();

            // キーボードデバイスが存在することを確認
            // Verify keyboard device exists
            Assert.IsNotNull(Keyboard.current, "No keyboard device found. Cannot test input injection.");

            // 安定のため数フレーム待機
            // Wait several frames for stability
            for (var i = 0; i < 10; i++) yield return null;

            // Space キーを注入し、即座にイベントを処理させる
            // Inject Space key and immediately process the event
            OsInputSpoof.KeyDown(OsInputSpoof.DebugKey.Space);
            InputSystem.Update();

            var spacePressed = false;
            for (var i = 0; i < 10 && !spacePressed; i++)
            {
                yield return null;
                spacePressed = Keyboard.current.spaceKey.isPressed;
            }

            // キーを離す（キー状態をクリア）
            // Release key to clear key state
            OsInputSpoof.KeyUp(OsInputSpoof.DebugKey.Space);

            Assert.IsTrue(spacePressed,
                "Space key was injected but Unity Input System did not detect it.");
        }

        /// <summary>
        /// W/A/S/D キーを順に注入し、それぞれ Unity Input System が検知することを確認する
        /// Inject W/A/S/D keys in sequence and verify Unity Input System detects each.
        /// </summary>
        [UnityTest]
        public IEnumerator OsKeyInjection_WASDKeys_AreDetectedByInputSystem()
        {
            // OS 入力権限チェック（使用不可なら Assert.Ignore でスキップ）
            // Check OS input permission (skip with Assert.Ignore if unavailable)
            OsInputSpoof.AssertAvailableOrSkip();

            Assert.IsNotNull(Keyboard.current, "No keyboard device found.");

            // 安定のため数フレーム待機
            // Wait several frames for stability
            for (var i = 0; i < 10; i++) yield return null;

            // WASD 各キーを注入して検知を確認
            // Inject and verify each WASD key
            yield return AssertKeyDetected(OsInputSpoof.DebugKey.W, () => Keyboard.current.wKey.isPressed, "W");
            yield return AssertKeyDetected(OsInputSpoof.DebugKey.A, () => Keyboard.current.aKey.isPressed, "A");
            yield return AssertKeyDetected(OsInputSpoof.DebugKey.S, () => Keyboard.current.sKey.isPressed, "S");
            yield return AssertKeyDetected(OsInputSpoof.DebugKey.D, () => Keyboard.current.dKey.isPressed, "D");

            #region Internal

            IEnumerator AssertKeyDetected(OsInputSpoof.DebugKey key, System.Func<bool> isPressed, string keyName)
            {
                // キー注入 → 即座にイベント処理 → ポーリング → 検知確認 → キー解放
                // Inject → immediately process event → poll → check detection → release
                OsInputSpoof.KeyDown(key);
                InputSystem.Update();

                var detected = false;
                for (var i = 0; i < 10 && !detected; i++)
                {
                    yield return null;
                    detected = isPressed();
                }

                OsInputSpoof.KeyUp(key);

                // 次のキーと干渉しないよう2フレーム空ける
                // Wait 2 frames before next key to avoid interference
                yield return null;
                yield return null;

                Assert.IsTrue(detected,
                    $"Key '{keyName}' was injected but not detected by Unity Input System.");
            }

            #endregion
        }

        /// <summary>
        /// マウスの相対移動を注入し、マウス位置が変化することを確認する
        /// Inject relative mouse movement and verify mouse position changes.
        /// </summary>
        [UnityTest]
        public IEnumerator OsMouseInjection_MouseMove_ChangesPosition()
        {
            // OS 入力権限チェック（使用不可なら Assert.Ignore でスキップ）
            // Check OS input permission (skip with Assert.Ignore if unavailable)
            OsInputSpoof.AssertAvailableOrSkip();

            Assert.IsNotNull(Mouse.current, "No mouse device found.");

            // 安定のため数フレーム待機
            // Wait several frames for stability
            for (var i = 0; i < 10; i++) yield return null;

            // 注入前のマウス位置を記録
            // Record mouse position before injection
            var positionBefore = Mouse.current.position.ReadValue();

            // マウスを (50, 0) 方向に移動し、即座にイベントを処理させる
            // Move mouse 50 pixels right and immediately process the event
            const int moveDelta = 50;
            OsInputSpoof.MouseMoveBy(moveDelta, 0);
            InputSystem.Update();

            // 最大10フレームポーリングしてマウス移動を確認
            // Poll up to 10 frames to verify mouse movement
            var moved = false;
            for (var i = 0; i < 10 && !moved; i++)
            {
                yield return null;
                var currentDelta = Mouse.current.delta.ReadValue();
                var currentPos   = Mouse.current.position.ReadValue();
                moved = currentDelta != Vector2.zero || Vector2.Distance(positionBefore, currentPos) > 1f;
            }

            // マウスが移動したことを確認
            // Verify mouse moved
            Assert.IsTrue(moved,
                $"Mouse did not move after injection. Before={positionBefore}.");
        }
    }
}
