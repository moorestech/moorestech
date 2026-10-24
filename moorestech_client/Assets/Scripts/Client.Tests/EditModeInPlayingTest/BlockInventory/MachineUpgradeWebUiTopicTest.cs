using System;
using System.Collections;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Tests.EditModeInPlayingTest.Util;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Client.WebUiHost.Game.Topics.MachineUpgrade;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Context;
using Game.Research;
using Mooresmaster.Model.BlocksModule;
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
    ///     研究の解放がイベント→クライアントのミラー→Web UIの配信まで届くことを、実ゲームの起動経路で確かめる
    ///     Verifies, through the real game boot, that research unlocks reach the Web UI topics via events and client mirrors
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay2")]
    public class MachineUpgradeWebUiTopicTest
    {
        private const string MachineBlockName = "キャンプファイア";
        private static readonly Guid InheritRecipeGuid = Guid.Parse("00000000-0000-4000-8000-0000e0de0001");
        private static readonly Guid MachineUpgradeResearchGuid = Guid.Parse("e0de0000-0000-4000-8000-000000000001");

        [UnityTest]
        public IEnumerator 研究でスロット上限と製作上限がWebUIの配信へ届く()
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

                // 機械を置いて開き、Web UIと同じ登録済みハンドラで継承レシピを選ぶ
                // Place and open the machine, then select the inheritance recipe through the Web UI's registered handler
                var pos = new Vector3Int(0, 0, 0);
                PlaceBlock(MachineBlockName, pos, BlockDirection.North);
                var blockGameObject = await WaitBlockGameObjectSpawn(pos);
                var subInventoryState = await BlockSubInventoryOpener.Open(blockGameObject);
                var select = await WebUiActionInvoker.ExecuteAsync("machine_recipe.select", new JObject { ["operation"] = "set", ["recipeGuid"] = InheritRecipeGuid.ToString() });
                Assert.IsTrue(select.Ok, $"select action failed: {select.Error}");

                // 研究前: スロット上限3・製作上限Lv1・継承元スロットは3変種を受け付ける
                // Before research: slot limit 3, craft cap Lv1, and the source slot accepts all three variants
                Assert.AreEqual(3, await ModuleSlotStackLimit());
                var levelInheritance = (await InheritRecipe())["levelInheritance"];
                Assert.AreEqual("inheritUpToUnlocked", levelInheritance["kind"].Value<string>());
                Assert.AreEqual(1, levelInheritance["maxCraftableLevel"].Value<int>());
                Assert.AreEqual(3, await SourceSlotAcceptedCount(blockGameObject));

                // サーバーで研究を完了させ、2つのtopicが再配信されるまで待つ
                // Complete the research server-side and wait until both topics are republished
                var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
                Assert.IsTrue(ServerContext.GetService<IResearchDataStore>().CompleteResearch(MachineUpgradeResearchGuid, playerId), "research did not complete");

                var stackLimit = await ModuleSlotStackLimit();
                for (var i = 0; i < 50 && stackLimit != 6; i++)
                {
                    await UniTask.Delay(100);
                    stackLimit = await ModuleSlotStackLimit();
                }
                Assert.AreEqual(6, stackLimit, "slot limit did not reach the Web UI topic");

                var maxCraftableLevel = (await InheritRecipe())["levelInheritance"]["maxCraftableLevel"].Value<int>();
                for (var i = 0; i < 50 && maxCraftableLevel != 2; i++)
                {
                    await UniTask.Delay(100);
                    maxCraftableLevel = (await InheritRecipe())["levelInheritance"]["maxCraftableLevel"].Value<int>();
                }
                Assert.AreEqual(2, maxCraftableLevel, "craft cap did not reach the Web UI topic");

                // 研究ノードの配信に解放2種の中身が載る（研究画面「解放: その他」の元データ。スロット上限はレベル1の上限値6）
                // The research node payload carries both unlocks (the source of the research screen's "Unlocks: Others"; the slot limit is level 1's value 6)
                var upgradeNode = await ResearchNode();
                CollectionAssert.AreEqual(new[] { 6 }, upgradeNode["unlockModuleSlotStackLimits"].ToObject<int[]>());
                var craftCap = (JObject)((JArray)upgradeNode["unlockLevelInheritanceCraftCaps"])[0];
                var productLv1ItemId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-e0de-000000000021")).AsPrimitive();
                Assert.AreEqual(productLv1ItemId, craftCap["itemId"].Value<int>());
                Assert.AreEqual(2, craftCap["level"].Value<int>());

                BlockSubInventoryOpener.Close(subInventoryState);
            }

            // 登録済みtopicのsnapshotをJSONで読む
            // Read a registered topic's snapshot as JSON
            async UniTask<JObject> Snapshot(string topicName)
            {
                var hub = Client.WebUiHost.Boot.WebUiHost.Hub;
                Assert.IsNotNull(hub, "WebUiHost hub is not running");
                var handler = hub.ResolveTopic(topicName);
                Assert.IsNotNull(handler, $"topic '{topicName}' is not registered by WebUiGameBinder");
                return JObject.Parse(await handler.GetSnapshotJsonAsync());
            }

            async UniTask<int> ModuleSlotStackLimit()
            {
                return (await Snapshot(MachineUpgradeTopic.TopicName))["moduleSlotStackLimit"].Value<int>();
            }

            async UniTask<JObject> ResearchNode()
            {
                foreach (var node in (JArray)(await Snapshot(ResearchTopic.TopicName))["nodes"])
                {
                    if (node["guid"].Value<string>() == MachineUpgradeResearchGuid.ToString()) return (JObject)node;
                }
                Assert.Fail("machine upgrade research is not in research.tree");
                return null;
            }

            async UniTask<JObject> InheritRecipe()
            {
                foreach (var recipe in (JArray)(await Snapshot(MachineRecipesTopic.TopicName))["recipes"])
                {
                    if (recipe["recipeGuid"].Value<string>() == InheritRecipeGuid.ToString("D")) return (JObject)recipe;
                }
                Assert.Fail("inheritance recipe is not in crafting.machine_recipes");
                return null;
            }

            // 選択レシピがブロック状態の同期で届くまで待ち、配信と同じ組み立てで継承元スロットの受け付けID数を返す
            // Wait for the selected recipe to arrive with the block-state sync and count the source slot's accepted IDs with the topic's own builder
            async UniTask<int> SourceSlotAcceptedCount(BlockGameObject blockGameObject)
            {
                // テストの開き方はUI状態をSubInventoryにしないためblock_inventory.currentは閉じたまま。topicと同じDTO組み立てを直に呼ぶ
                // The test's opener leaves the UI state outside SubInventory so block_inventory.current stays closed; call the topic's DTO builder directly
                var selectedRecipeGuid = SelectedRecipeGuid(blockGameObject);
                for (var i = 0; i < 50 && selectedRecipeGuid != InheritRecipeGuid; i++)
                {
                    await UniTask.Delay(100);
                    selectedRecipeGuid = SelectedRecipeGuid(blockGameObject);
                }
                Assert.AreEqual(InheritRecipeGuid, selectedRecipeGuid, "selected recipe never arrived with the block-state sync");

                var selectedRecipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(selectedRecipeGuid);
                var machineParam = (IMachineParam)blockGameObject.BlockMasterElement.BlockParam;
                var bindings = MachineSlotBindingDtoBuilder.BuildSlotBindings(selectedRecipe, machineParam);
                return bindings[0].AcceptedItemIds.Count;
            }

            Guid SelectedRecipeGuid(BlockGameObject blockGameObject)
            {
                var machineState = blockGameObject.GetStateDetail<MachineBlockStateDetail>(MachineBlockStateDetail.BlockStateDetailKey);
                return machineState == null ? Guid.Empty : Guid.Parse(machineState.SelectedRecipeGuid);
            }

            #endregion
        }
    }
}
