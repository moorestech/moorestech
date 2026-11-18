using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Client.Tests.EditModeInPlayingTest.Util;
using Client.Tests.EditModeInPlayingTest.Util.World;
using Client.Ui.State.BlockInventory.Special.Commands;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;
using static Client.Tests.EditModeInPlayingTest.Ui.UiStateTransitionWait;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    /// <summary>
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// This test is executed in EditMode, but it switches to PlayMode during execution.
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay2")]
    public class ElectricToGearOutputModeActionTest
    {
        private const string BlockName = "TestElectricToGearGeneratorUI";

        [UnityTest]
        public IEnumerator 出力モード選択がサーバーのSelectedIndexへ届く()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
#if UNITY_EDITOR
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
#endif

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                // 開いた画面は届く範囲を外れると閉じるので、足場を置く前に自機を対象の -x の隣へ移す
                // The opened screen closes once out of reach, so move the player next to the target on -x before placing the ground
                var pos = new Vector3Int(0, 0, 0);
                PlayerSystemContainer.Instance.PlayerObjectController.SetPlayerPosition(pos + new Vector3(-1.5f, 0.1f, 0.5f));
                var ground = TestPlayerGround.Create();

                // 実足場を置き想定外エラーを検出
                // Place real ground and detect unexpected errors
                await UniTask.Yield();
                LogAssert.ignoreFailingMessages = false;
                try
                {
                    await TestPlayerGround.AssertGroundedAsync();
                    // ブロック配置し初期選択0を確認
                    // Place the block and confirm initial selection 0
                    var serverBlock = await ServerBlockPlacement.PlaceAsync(BlockName, pos, BlockDirection.North);
                    var component = serverBlock.GetComponent<ElectricToGearGeneratorComponent>();
                    Assert.AreEqual(0, component.SelectedIndex);

                    var blockGameObject = await WaitBlockGameObjectSpawn(pos);
                    // UITK のパネルは画面が SubInventory のときだけ開くので、先に遷移してから実プレイと同じ文脈で開く（前例 GameUiMachineSectionWiringTest。文脈なしのエラーログが 1 回出る）
                    // The UITK panel opens only on the SubInventory screen, so transition first and reopen with the real-play context (precedent GameUiMachineSectionWiringTest; one no-context error log)
                    var uiStateControl = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();
                    LogAssert.Expect(LogType.Error, "SubInventoryState: サブインベントリソースが指定されていません");
                    await TransitionTo(uiStateControl, UIStateEnum.SubInventory);
                    var subInventoryState = await BlockSubInventoryOpener.Open(blockGameObject);

                    // 特殊族コマンドで出力モードを選ぶ
                    // Pick the output mode via the special-family command
                    var commands = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ISpecialBlockCommands>();
                    commands.SetElectricToGearMode(2);

                    // 往復後の選択2を確認（結果はトーストの係へ行く。拒否なら [UiCommandResultToaster] の警告ログに理由が出る）
                    // Confirm selection 2 after the round-trip (results go to the toaster; a rejection's reason appears in the [UiCommandResultToaster] warning log)
                    for (var i = 0; i < 50 && component.SelectedIndex != 2; i++) await UniTask.Delay(100);
                    Assert.AreEqual(2, component.SelectedIndex, "row select did not reach server SelectedIndex; see the [UiCommandResultToaster] electric_to_gear.set_output_mode warning for a rejection");

                    BlockSubInventoryOpener.Close(subInventoryState);
                    await TransitionTo(uiStateControl, UIStateEnum.GameScreen);

                    // assert後に保存・終了待ち
                    // After asserts, await save and shutdown
                    await AssertIntentionalShutdownFlushedAsync("ElectricToGearShutdown");
                }
                finally
                {
                    Object.DestroyImmediate(ground);
                    LogAssert.ignoreFailingMessages = true;
                }
            }

            #endregion
        }
    }
}
