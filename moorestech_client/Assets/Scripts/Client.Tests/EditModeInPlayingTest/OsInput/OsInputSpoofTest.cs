using System.Collections;
using Client.Tests.EditModeInPlayingTest.OsInput;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// OsInputSpoof による入力合成を EditMode で検証するテスト
    /// EditMode test that verifies input injection via OsInputSpoof.
    ///
    /// macOS Editor では InputSystem.QueueStateEvent を使用するため PlayMode 不要
    /// On macOS Editor, uses InputSystem.QueueStateEvent so PlayMode is not required.
    ///
    /// 前提（macOS Standalone）: システム設定 → プライバシーとセキュリティ → アクセシビリティ で Unity を許可すること
    /// Prerequisite (macOS Standalone): Grant Unity permission in System Settings → Privacy & Security → Accessibility.
    /// </summary>
    public class OsInputSpoofTest
    {
        /// <summary>
        /// テストで使用する全キーを強制解放し、キー状態のリークを防止する
        /// Force-release all test keys to prevent key state leaks across tests.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var key in new[]
            {
                OsInputSpoof.DebugKey.Space,
                OsInputSpoof.DebugKey.W, OsInputSpoof.DebugKey.A,
                OsInputSpoof.DebugKey.S, OsInputSpoof.DebugKey.D,
            })
            {
                OsInputSpoof.KeyUp(key);
            }
            InputSystem.Update();
        }

        private const string AccessibilityIgnoreMessage =
            "Accessibility permission is not granted. " +
            "Grant permission and restart Unity, then rerun. See error log for details.";

        private const string AccessibilityDialogTitle   = "Accessibility Permission Required";
        private const string AccessibilityDialogMessage =
            "OS レベルキー注入には macOS Accessibility 権限が必要です。\n" +
            "OS-level key injection requires macOS Accessibility permission.\n\n" +
            "設定場所 / Where to grant:\n" +
            "  システム設定 → プライバシーとセキュリティ → アクセシビリティ\n" +
            "  System Settings → Privacy & Security → Accessibility\n\n" +
            "Unity Editor を追加してチェックを入れ、Unity を再起動してからテストを再実行してください。\n" +
            "Add Unity Editor, enable the toggle, restart Unity, then rerun the tests.";

        /// <summary>
        /// Space キーを注入し、Unity Input System が検知することを確認する
        /// Inject Space key and verify Unity Input System detects it.
        /// </summary>
        [UnityTest]
        public IEnumerator OsKeyInjection_SpaceKey_IsDetectedByInputSystem()
        {
            // Accessibility 権限チェック
            // Check Accessibility permission
            if (!OsInputSpoof.IsAvailable)
            {
                NotifyAccessibilityRequired();
                Assert.Ignore(AccessibilityIgnoreMessage);
                yield break;
            }

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
            // Accessibility 権限チェック
            // Check Accessibility permission
            if (!OsInputSpoof.IsAvailable)
            {
                NotifyAccessibilityRequired();
                Assert.Ignore(AccessibilityIgnoreMessage);
                yield break;
            }

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
            // Accessibility 権限チェック
            // Check Accessibility permission
            if (!OsInputSpoof.IsAvailable)
            {
                NotifyAccessibilityRequired();
                Assert.Ignore(AccessibilityIgnoreMessage);
                yield break;
            }

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

        /// <summary>
        /// Accessibility 権限が必要であることをコンソールエラーとモーダルで通知する
        /// Notify that Accessibility permission is required via console error and modal dialog.
        /// </summary>
        private static void NotifyAccessibilityRequired()
        {
            // macOS Standalone のみ: システムダイアログで権限を要求
            // macOS Standalone only: request via macOS system dialog
#if UNITY_STANDALONE_OSX
            OsInputSpoof.RequestMacAccessibility();
#endif

            // Unity コンソールにエラーログを出力
            // Output error log to Unity console
            Debug.LogError(
                "[OsInputSpoofTest] Accessibility permission is required for OS-level key injection.\n" +
                "Grant permission in: System Settings → Privacy & Security → Accessibility\n" +
                "Add Unity Editor to the list, enable the toggle, restart Unity, then rerun the tests.");

            // Unity エディタのモーダルダイアログを表示
            // Show modal dialog in Unity Editor
            EditorUtility.DisplayDialog(AccessibilityDialogTitle, AccessibilityDialogMessage, "OK");
        }
    }
}
