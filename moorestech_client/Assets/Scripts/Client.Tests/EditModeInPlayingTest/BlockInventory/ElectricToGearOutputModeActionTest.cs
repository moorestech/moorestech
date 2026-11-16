using System.Collections;
using Client.Game.InGame.Context;
using Client.Tests.EditModeInPlayingTest.Util;
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
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

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
            BlockActionSceneIsolation.Prepare();

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
                BlockActionSceneIsolation.AssertIsolatedSceneIsReady();
                // 検出境界を保存終了まで保つ
                // Keep error detection until shutdown saves
                LogAssert.ignoreFailingMessages = false;
                try
                {
                    // ブロック配置し初期選択0を確認
                    // Place the block and confirm initial selection 0
                    var pos = new Vector3Int(0, 0, 0);
                    var serverBlock = await ServerBlockPlacement.PlaceAsync(BlockName, pos, BlockDirection.North);
                    var component = serverBlock.GetComponent<ElectricToGearGeneratorComponent>();
                    Assert.AreEqual(0, component.SelectedIndex);

                    var blockGameObject = await WaitBlockGameObjectSpawn(pos);
                    var subInventoryState = await BlockSubInventoryOpener.Open(blockGameObject);

                    // UITK の特殊族のコマンドで出力モードを選ぶ（画面のボタンと同じ口）
                    // Pick the output mode through the UITK special-family command (the same port the screen's button uses)
                    var commands = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ISpecialBlockCommands>();
                    var result = await commands.SetElectricToGearMode(2);
                    Assert.IsInstanceOf<UiCommandAccepted>(result, $"output mode command was rejected: {(result as UiCommandRejected)?.ToLogText()}");

                    // 往復後の選択2を確認
                    // Confirm selection 2 after the round-trip
                    for (var i = 0; i < 120 && component.SelectedIndex != 2; i++) await UniTask.Yield();
                    Assert.AreEqual(2, component.SelectedIndex, "row select did not reach server SelectedIndex");

                    BlockSubInventoryOpener.Close(subInventoryState);

                    // assert後に保存・終了待ち
                    // After asserts, await save and shutdown
                    await AssertIntentionalShutdownFlushedAsync("ElectricToGearShutdown");
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = true;
                }
            }

            #endregion
        }

        [TearDown]
        public void ResetSceneIsolation() => BlockActionSceneIsolation.Reset();
    }
}
