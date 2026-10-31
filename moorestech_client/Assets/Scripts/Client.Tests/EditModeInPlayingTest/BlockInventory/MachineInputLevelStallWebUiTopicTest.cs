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
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
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

            // テスト後にデバッグ無効化を解除
            // Clear debug disabling after the test
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

                // 上限つき継承レシピを通常のUI経路で選ぶ
                // Select the capped inheritance recipe through the production UI path
                var select = await WebUiActionInvoker.ExecuteAsync("machine_recipe.select", new JObject { ["operation"] = "set", ["recipeGuid"] = CrusherInheritRecipeGuid.ToString() });
                Assert.IsTrue(select.Ok, $"select action failed: {select.Error}");

                // 製作上限 Lv1 の間に Lv2 の素材をチェストへ入れると、ベルトの先頭で止まり、理由が配信に載る
                // Putting a Lv2 material in the chest under the Lv1 craft cap stalls it at the belt head and the reason reaches the topic
                var sourceLv2 = MasterHolder.ItemMaster.GetItemId(SourceLv2ItemGuid);
                var feed = new MachineInputStallFeedSetupStep(machinePos, sourceLv2);
                await feed.RunOnServerAsync();
                Assert.IsNotNull(feed.Chest, "the feed chest was not placed");
                Assert.IsNotNull(feed.Belt, "the feed belt was not placed");
                Assert.AreEqual(0, feed.RemainingSourceCount, "the Lv2 source did not fit in the chest");
                var stall = await WaitUntilStallAppears();
                Assert.AreEqual("aboveCraftCap", stall["reason"].Value<string>());
                Assert.AreEqual(sourceLv2.AsPrimitive(), stall["itemId"].Value<int>());

                // 研究で製作上限を2へ上げると、待っていた Lv2 が入り、理由が配信から消える
                // Raising the craft cap to 2 by research lets the waiting Lv2 in and the reason leaves the topic
                var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
                var research = new MachineInputStallResearchStep(MachineUpgradeResearchGuid, playerId);
                await research.RunOnServerAsync();
                Assert.IsTrue(research.Completed, "research did not complete");
                await WaitUntilStallCleared();

                // 加工後の継承出力IDと個数を検証
                // Verify the inherited output ID and count after processing
                var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(CrusherInheritRecipeGuid);
                var processor = machine.GetComponent<VanillaMachineProcessorComponent>();
                var inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();
                var outputSlot = ((Mooresmaster.Model.BlocksModule.IMachineParam)machine.BlockMasterElement.BlockParam).InputSlotCount;
                var outputId = MachineRecipeLevelInheritanceUtil.ResolveOutputItemId(recipe, 0, 2);
                for (var frame = 0; frame < 3600 && inventory.GetItem(outputSlot).Count == 0; frame++)
                {
                    processor.SupplyExternalPower(processor.EffectiveRequestPower);
                    await UniTask.Yield();
                }
                var output = inventory.GetItem(outputSlot);
                Assert.AreEqual(outputId, output.Id);
                Assert.AreEqual(recipe.OutputItems[0].Count, output.Count);
                Assert.AreEqual(ItemMaster.EmptyItemId, feed.Chest.GetComponent<IOpenableBlockInventoryComponent>().GetItem(0).Id, "the Lv2 is still in the chest");

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

            // ベルトの搬送と1秒ごとの問い合わせを待ち、取得済みの止まった理由が配信に載るまで待つ
            // Wait for the belt transport and the 1 s query until a fetched stall reaches the topic
            async UniTask<JObject> WaitUntilStallAppears()
            {
                var firstTick = Core.Update.GameUpdater.CurrentTick;
                var stall = await FetchedStall();
                for (var i = 0; i < 100 && stall == null; i++)
                {
                    await UniTask.Delay(100);
                    stall = await FetchedStall();
                }
                Assert.IsNotNull(stall, $"a fetched machineInputStall never reached the block inventory topic; ticks: {firstTick}->{Core.Update.GameUpdater.CurrentTick}; latest topic: {await BlockInventorySnapshot()}");
                return stall;
            }

            // 止まった理由が配信から消えるまで待つ（取得済みで止まっていなければ欄ごと無い）
            // Wait until the stall leaves the topic; once fetched and not stalled, the section is absent
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

            // 取得済みの止まった理由だけを返す。取得中・取得失敗の欄は null 扱い
            // Returns only a fetched stall; loading or failed sections count as null
            async UniTask<JObject> FetchedStall()
            {
                var stall = (await BlockInventorySnapshot())["machineInputStall"] as JObject;
                return stall?["fetchState"]?.Value<string>() == "fetched" ? stall : null;
            }

            #endregion
        }
    }
}
