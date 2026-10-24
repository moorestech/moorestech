using System;
using System.Collections;
using Client.Game.InGame.Context;
using Client.Tests.EditModeInPlayingTest.Util;
using Client.WebUiHost.Game.Topics;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Research;
using Newtonsoft.Json.Linq;
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
    ///     ベルトで届いた製作上限超えの素材で止まった理由が、実ゲームの起動経路で Web UI の配信に載り、研究で消えることを確かめる
    ///     Verifies, through the real game boot, that the stall from an above-cap material arriving by belt reaches the Web UI topic and clears after research
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay2")]
    public class MachineInputLevelStallWebUiTopicTest
    {
        private const string MachineBlockName = "石の粉砕機";
        private const string BeltBlockName = "直進高速ベルトコンベア";
        private const string ChestBlockName = "量子チェスト";
        private static readonly Guid CrusherInheritRecipeGuid = Guid.Parse("00000000-0000-4000-8000-0000e0de0002");
        private static readonly Guid MachineUpgradeResearchGuid = Guid.Parse("e0de0000-0000-4000-8000-000000000001");
        private static readonly Guid SourceLv2ItemGuid = Guid.Parse("00000000-0000-0000-e0de-000000000012");

        [UnityTest]
        public IEnumerator 製作上限を超える素材がベルトで届くと止まった理由が配信に載り研究で消える()
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

                // 粉砕機を手の届く所に置いてFで開く（配信の open は本番のUI遷移でだけ立つ）
                // Place the crusher within reach and open it with F (the topic's open flag is only set through the production UI transition)
                var machineObject = await InteractKeyBlockOpener.PlaceNearPlayerAndOpen(MachineBlockName);
                var machinePos = machineObject.BlockPosInfo.OriginalPos;
                var machine = ServerContext.WorldBlockDatastore.GetBlock(machinePos);

                // 粉砕機の南へチェスト→ベルトを北向きに一列に置き、上限つき継承レシピを選ぶ
                // Line chest -> belt facing north south of the crusher and select the capped inheritance recipe
                var chest = PlaceBlock(ChestBlockName, machinePos + new Vector3Int(0, 0, -2), BlockDirection.North);
                PlaceBlock(BeltBlockName, machinePos + new Vector3Int(0, 0, -1), BlockDirection.North);
                var select = await WebUiActionInvoker.ExecuteAsync("machine_recipe.select", new JObject { ["operation"] = "set", ["recipeGuid"] = CrusherInheritRecipeGuid.ToString() });
                Assert.IsTrue(select.Ok, $"select action failed: {select.Error}");

                // 製作上限 Lv1 の間に Lv2 の素材をチェストへ入れると、ベルトの先頭で止まり、理由が配信に載る
                // Putting a Lv2 material in the chest under the Lv1 craft cap stalls it at the belt head and the reason reaches the topic
                var sourceLv2 = MasterHolder.ItemMaster.GetItemId(SourceLv2ItemGuid);
                InsertItemToBlock(chest, sourceLv2, 1);
                var stall = await WaitUntilStallAppears();
                Assert.AreEqual("aboveCraftCap", stall["reason"].Value<string>());
                Assert.AreEqual(sourceLv2.AsPrimitive(), stall["itemId"].Value<int>());

                // 研究で製作上限を2へ上げると、待っていた Lv2 が入り、理由が配信から消える
                // Raising the craft cap to 2 by research lets the waiting Lv2 in and the reason leaves the topic
                var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
                Assert.IsTrue(ServerContext.GetService<IResearchDataStore>().CompleteResearch(MachineUpgradeResearchGuid, playerId), "research did not complete");
                await WaitUntilStallCleared();

                // 待っていた Lv2 は継承元スロットへ入る。この粉砕機は歯車を繋がなくても加工を始める（実測）ので、届く品が Lv2 だけのこの場面では加工中も Lv2 を受けた証になる
                // The waiting Lv2 enters the source slot; this crusher starts a job without gears (observed), so with only Lv2 arriving here a running job also proves the Lv2 was taken
                var sourceSlotItem = machine.GetComponent<IOpenableBlockInventoryComponent>().GetItem(0).Id;
                var processState = machine.GetComponent<VanillaMachineProcessorComponent>().CurrentState;
                Assert.IsTrue(sourceSlotItem == sourceLv2 || processState == ProcessState.Processing, $"the waiting Lv2 did not enter the source slot (slot0={sourceSlotItem}, state={processState})");
                Assert.AreEqual(ItemMaster.EmptyItemId, chest.GetComponent<IOpenableBlockInventoryComponent>().GetItem(0).Id, "the Lv2 is still in the chest");

                await InteractKeyBlockOpener.CloseToGameScreen();
            }

            // 開いているブロックの配信を JSON で読む
            // Read the open block's topic snapshot as JSON
            async UniTask<JObject> BlockInventorySnapshot()
            {
                var hub = Client.WebUiHost.Boot.WebUiHost.Hub;
                Assert.IsNotNull(hub, "WebUiHost hub is not running");
                var handler = hub.ResolveTopic(BlockInventoryTopic.TopicName);
                Assert.IsNotNull(handler, $"topic '{BlockInventoryTopic.TopicName}' is not registered by WebUiGameBinder");
                return JObject.Parse(await handler.GetSnapshotJsonAsync());
            }

            // ベルトの搬送と1秒ごとの問い合わせを待ち、止まった理由が配信に載るまで待つ
            // Wait for the belt transport and the 1 s query until the stall reaches the topic
            async UniTask<JObject> WaitUntilStallAppears()
            {
                var stall = (await BlockInventorySnapshot())["machineInputStall"] as JObject;
                for (var i = 0; i < 100 && stall == null; i++)
                {
                    await UniTask.Delay(100);
                    stall = (await BlockInventorySnapshot())["machineInputStall"] as JObject;
                }
                Assert.IsNotNull(stall, "machineInputStall never reached the block inventory topic");
                return stall;
            }

            // 止まった理由が配信から消えるまで待つ
            // Wait until the stall leaves the topic
            async UniTask WaitUntilStallCleared()
            {
                var stall = (await BlockInventorySnapshot())["machineInputStall"];
                for (var i = 0; i < 50 && stall != null; i++)
                {
                    await UniTask.Delay(100);
                    stall = (await BlockInventorySnapshot())["machineInputStall"];
                }
                Assert.IsNull(stall, "machineInputStall did not clear after the craft cap was raised");
            }

            #endregion
        }
    }
}
