using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Tests.EditModeInPlayingTest.Util;
using Client.WebUiHost.Game.Actions;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.UnlockState;
using Mooresmaster.Model.MachineRecipesModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
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
    public class MachineRecipeSelectRoundTripTest
    {
        private const string MachineBlockName = "釜";

        [UnityTest]
        public IEnumerator レシピ選択がサーバーの選択レシピへ届く()
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

            // デバッグ無効化フラグをクリア
            // Clear debug objects disabled flag after test.
#if UNITY_EDITOR
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
#endif

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                // 機械を設置し、初期状態でレシピ未選択であることを確認
                // Place the machine and confirm no recipe is selected initially.
                var pos = new Vector3Int(0, 0, 0);
                var serverBlock = PlaceBlock(MachineBlockName, pos, BlockDirection.North);
                var processor = serverBlock.GetComponent<VanillaMachineProcessorComponent>();
                Assert.AreEqual(System.Guid.Empty, processor.SelectedRecipeGuid, "recipe already selected before the test acted");

                var blockGameObject = await WaitBlockGameObjectSpawn(pos);

                // このブロックのレシピをマスタから引く（テストマスタでは2件）
                // Derive the block's recipes from the master (two in the test master data).
                var blockGuid = blockGameObject.BlockMasterElement.BlockGuid;
                var blockRecipes = new List<MachineRecipeMasterElement>();
                foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
                    if (recipe.BlockGuid == blockGuid) blockRecipes.Add(recipe);
                Assert.AreEqual(2, blockRecipes.Count, "test master data recipe count mismatch");

                var subInventoryState = await BlockSubInventoryOpener.Open(blockGameObject);
                var unlockStateData = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IGameUnlockStateData>();
                var handler = new MachineRecipeSelectActionHandler(subInventoryState, unlockStateData);

                // Web UIと同じ action handler 経由で1件目を選択する
                // Select the first recipe through the same action handler the Web UI uses.
                var targetGuid = blockRecipes[0].MachineRecipeGuid;
                var setResult = await handler.ExecuteAsync(new JObject { ["operation"] = "set", ["recipeGuid"] = targetGuid.ToString() });
                Assert.IsTrue(setResult.Ok, $"select action failed: {setResult.Error}");
                Assert.AreEqual(targetGuid, processor.SelectedRecipeGuid, "selected recipe did not reach the server");

                // 解除も同じ経路で往復することを確認する
                // Confirm clearing round-trips through the same path.
                var clearResult = await handler.ExecuteAsync(new JObject { ["operation"] = "clear" });
                Assert.IsTrue(clearResult.Ok, $"clear action failed: {clearResult.Error}");
                Assert.AreEqual(System.Guid.Empty, processor.SelectedRecipeGuid, "clear did not reach the server");

                subInventoryState.OnExit();
            }

            #endregion
        }
    }
}
