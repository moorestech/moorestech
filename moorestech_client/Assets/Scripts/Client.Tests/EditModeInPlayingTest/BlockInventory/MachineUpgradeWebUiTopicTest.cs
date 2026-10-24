using System;
using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Tests.EditModeInPlayingTest.Util;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.MachineUpgrade;
using Core.Master;
using Cysharp.Threading.Tasks;
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

            // テスト後にデバッグ無効化を解除
            // Clear debug disabling after the test
#if UNITY_EDITOR
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
#endif

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                // 手の届く所に機械を置いてFで開き、Web UIと同じ登録済みハンドラで継承レシピを選ぶ
                // Place the machine within reach, open it with F, then select the inheritance recipe through the Web UI's registered handler
                await InteractKeyBlockOpener.PlaceNearPlayerAndOpen(MachineBlockName);
                var select = await WebUiActionInvoker.ExecuteAsync("machine_recipe.select", new JObject { ["operation"] = "set", ["recipeGuid"] = InheritRecipeGuid.ToString() });
                Assert.IsTrue(select.Ok, $"select action failed: {select.Error}");

                // 研究前: スロット上限3・製作上限Lv1・継承元スロットは3変種を受け付ける
                // Before research: slot limit 3, craft cap Lv1, and the source slot accepts all three variants
                Assert.AreEqual(3, await ModuleSlotStackLimit());
                var levelInheritance = (await InheritRecipe())["levelInheritance"];
                Assert.AreEqual("inheritUpToUnlocked", levelInheritance["kind"].Value<string>());
                Assert.AreEqual(1, levelInheritance["maxCraftableLevel"].Value<int>());
                Assert.AreEqual(3, await SourceSlotAcceptedCount());

                // 研究完了後に両トピックの再配信待ち
                // Wait for both topics to republish after research completes
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

                await InteractKeyBlockOpener.CloseToGameScreen();
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

            // 開いている機械の配信に選択レシピの束縛が載るまで待ち、継承元スロットの受け付けID数を返す
            // Wait until the open machine's payload carries the selected recipe's binding and return the source slot's accepted ID count
            async UniTask<int> SourceSlotAcceptedCount()
            {
                var current = await Snapshot(BlockInventoryTopic.TopicName);
                for (var i = 0; i < 50 && !HasSlotBinding(current); i++)
                {
                    await UniTask.Delay(100);
                    current = await Snapshot(BlockInventoryTopic.TopicName);
                }
                Assert.IsTrue(current["open"].Value<bool>(), $"block_inventory.current is not open: {current}");
                Assert.IsTrue(HasSlotBinding(current), $"slot bindings never arrived after selecting the recipe: {current}");
                var acceptedItemIds = ((JArray)current["machine"]["slotBindings"][0]["acceptedItemIds"]).ToObject<int[]>();
                Debug.Log($"[MachineUpgradeWebUiTopicTest] block_inventory.current open={current["open"]} acceptedItemIds=[{string.Join(",", acceptedItemIds)}]");

                // 受け付けるのは継承元ファミリーの素材Lv1..3そのもの
                // The accepted IDs are exactly the source family's material Lv1..3
                var sourceLevelItemIds = new List<int>();
                foreach (var guid in new[] { "00000000-0000-0000-e0de-000000000011", "00000000-0000-0000-e0de-000000000012", "00000000-0000-0000-e0de-000000000013" })
                    sourceLevelItemIds.Add(MasterHolder.ItemMaster.GetItemId(Guid.Parse(guid)).AsPrimitive());
                CollectionAssert.AreEquivalent(sourceLevelItemIds, acceptedItemIds);
                return acceptedItemIds.Length;
            }

            bool HasSlotBinding(JObject current)
            {
                return current["machine"]?["slotBindings"] is JArray { Count: > 0 };
            }

            #endregion
        }
    }
}
