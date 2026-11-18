using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Tests.EditModeInPlayingTest.BlockInventory;
using Client.Tests.EditModeInPlayingTest.Util.World;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;
using static Client.Tests.EditModeInPlayingTest.Ui.UiStateTransitionWait;

namespace Client.Tests.EditModeInPlayingTest.Ui.Crosscut
{
    /// <summary>
    /// GameUi の実ゲームでチェストを開いて Esc を押すとゲーム画面へ戻ることを確かめる（L3。Web e2e inputLayer.spec の写し）
    /// Verifies in the real GameUi game that Esc on an open chest returns to the game screen (L3; port of the Web e2e inputLayer.spec)
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class BlockInventoryEscapeGameUiTest
    {
        private const string ChestBlockName = "量子チェスト";
        private const int EscapeWaitFrameLimit = 120;

        /// <web-test file="e2e/tests/regression/inputLayer.spec.ts">block inventory上のEscapeはGameScreen遷移を要求する</web-test>
        [UnityTest]
        public IEnumerator チェストを開いてEscを押すとゲーム画面へ戻る()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return TestBody().ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                await LoadMainGame();
                var ground = TestPlayerGround.Create();
                await UniTask.Yield();
                LogAssert.ignoreFailingMessages = false;
                await TestPlayerGround.AssertGroundedAsync();
                var uiStateControl = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();

                // 到達範囲で閉じないよう自機の +x の隣にチェストを置いて開く（前例 GameUiMachineSectionWiringTest）
                // Place a chest next to the player on +x so the reach check keeps it open, then open it (precedent GameUiMachineSectionWiringTest)
                var playerPosition = Vector3Int.RoundToInt(PlayerSystemContainer.Instance.PlayerObjectController.Position);
                var chestPosition = playerPosition + new Vector3Int(1, 0, 0);
                PlaceBlock(ChestBlockName, chestPosition, BlockDirection.North);
                var chest = await WaitBlockGameObjectSpawn(chestPosition);
                Physics.SyncTransforms();
                LogAssert.Expect(LogType.Error, "SubInventoryState: サブインベントリソースが指定されていません");
                await TransitionTo(uiStateControl, UIStateEnum.SubInventory);
                await BlockSubInventoryOpener.Open(chest);
                Assert.AreEqual(UIStateEnum.SubInventory, uiStateControl.CurrentState, "チェストを開いた後に SubInventory でない");

                // Esc（閉じるキー）の押下を差し込み、ゲーム画面へ戻るまで待つ
                // Inject the Esc (close-UI key) press and wait until the game screen is back
                var closeUi = InputManager.UI.CloseUI;
                closeUi.SetKeyDownForTest(true);
                for (var i = 0; i < EscapeWaitFrameLimit && uiStateControl.CurrentState != UIStateEnum.GameScreen; i++) await UniTask.Yield();
                closeUi.SetKeyDownForTest(false);
                Assert.AreEqual(UIStateEnum.GameScreen, uiStateControl.CurrentState, "チェスト上の Esc でゲーム画面へ戻らない");

                Object.DestroyImmediate(ground);
                LogAssert.ignoreFailingMessages = true;
            }

            #endregion
        }
    }
}
