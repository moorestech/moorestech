using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Tests.EditModeInPlayingTest.Util;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Client.Ui.State.Commands;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Source;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Logic;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.Research;
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
    ///     研究の解放がイベント→クライアントのミラー→UITK のモデルまで届くことを、実ゲームの起動経路で確かめる
    ///     Verifies, through the real game boot, that research unlocks reach the UITK models via events and client mirrors
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay2")]
    public class MachineUpgradeSectionModelTest
    {
        private const string MachineBlockName = "キャンプファイア";
        private static readonly Guid InheritRecipeGuid = Guid.Parse("00000000-0000-4000-8000-0000e0de0001");
        private static readonly Guid MachineUpgradeResearchGuid = Guid.Parse("e0de0000-0000-4000-8000-000000000001");

        [UnityTest]
        public IEnumerator 研究でスロット上限と製作上限が機械の欄のモデルへ届く()
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

                // 手の届く所に機械を置いてFで開き、画面の行と同じ UITK のコマンドで継承レシピを選ぶ
                // Place the machine within reach, open it with F, then select the inheritance recipe through the UITK command the screen's row uses
                await InteractKeyBlockOpener.PlaceNearPlayerAndOpen(MachineBlockName);
                var select = await Resolver().Resolve<IMachineRecipeSelectCommand>().Select(InheritRecipeGuid);
                Assert.IsInstanceOf<UiCommandAccepted>(select, $"select command was rejected: {(select as UiCommandRejected)?.ToLogText()}");

                // 研究前: スロット上限3・製作上限Lv1・継承元スロットは3変種を受け付ける
                // Before research: slot limit 3, craft cap Lv1, and the source slot accepts all three variants
                Assert.AreEqual(3, ModuleSlotStackLimit());
                var levelInheritance = InheritRecipeLevelInheritance();
                Assert.IsTrue(levelInheritance.CappedAtUnlockedLevel, "the inheritance recipe is not capped at the unlocked level");
                Assert.AreEqual(1, levelInheritance.MaxCraftableLevel);
                Assert.AreEqual(3, await SourceSlotAcceptedCount());

                // 研究完了後に両モデルの作り直しを待つ
                // Wait for both models to rebuild after research completes
                var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
                Assert.IsTrue(ServerContext.GetService<IResearchDataStore>().CompleteResearch(MachineUpgradeResearchGuid, playerId), "research did not complete");

                for (var i = 0; i < 50 && ModuleSlotStackLimit() != 6; i++) await UniTask.Delay(100);
                Assert.AreEqual(6, ModuleSlotStackLimit(), "slot limit did not reach the machine section model");

                for (var i = 0; i < 50 && InheritRecipeLevelInheritance().MaxCraftableLevel != 2; i++) await UniTask.Delay(100);
                Assert.AreEqual(2, InheritRecipeLevelInheritance().MaxCraftableLevel, "craft cap did not reach the unlocked machine recipe source");

                // 研究ノードの定義に解放2種の中身が載る（研究画面「解放: その他」の元データ。スロット上限はレベル1の上限値6）
                // The research node definition carries both unlocks (the source of the research screen's "Unlocks: Others"; the slot limit is level 1's value 6)
                var upgradeUnlocks = ResearchNodeUnlocks();
                CollectionAssert.AreEqual(new[] { 6 }, upgradeUnlocks.ModuleSlotStackLimits);
                var craftCap = upgradeUnlocks.LevelInheritanceCraftCaps[0];
                var productLv1ItemId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-e0de-000000000021")).AsPrimitive();
                Assert.AreEqual(productLv1ItemId, craftCap.ItemId.AsPrimitive());
                Assert.AreEqual(2, craftCap.Level);

                await InteractKeyBlockOpener.CloseToGameScreen();
            }

            IObjectResolver Resolver()
            {
                return ClientDIContext.DIContainer.DIContainerResolver;
            }

            // 開いている機械の区画の値を読む（開いていなければ落とす）
            // Read the open machine section's value (fail when none is open)
            MachineSectionViewModel MachineSection()
            {
                Assert.IsTrue(Resolver().Resolve<IMachineSectionModel>().Current.Value.TryGetPresent(out var machine), "the machine section is not open");
                return machine;
            }

            int ModuleSlotStackLimit()
            {
                return MachineSection().ModuleStackLimit;
            }

            // 解放済みの機械レシピから継承レシピのレベル継承を引く
            // Look up the inheritance recipe's level inheritance among the unlocked machine recipes
            LevelInheritanceMapping InheritRecipeLevelInheritance()
            {
                foreach (var recipe in Resolver().Resolve<UnlockedMachineRecipeSource>().Current.Value)
                {
                    if (recipe.RecipeGuid != InheritRecipeGuid) continue;
                    Assert.IsInstanceOf<LevelInheritanceMapping>(recipe.LevelInheritance, "the inheritance recipe carries no level inheritance");
                    return (LevelInheritanceMapping)recipe.LevelInheritance;
                }
                Assert.Fail("inheritance recipe is not in the unlocked machine recipes");
                return null;
            }

            ResearchUnlockSet ResearchNodeUnlocks()
            {
                foreach (var node in Resolver().Resolve<ResearchNodeCatalog>().Nodes)
                {
                    if (node.Guid == MachineUpgradeResearchGuid) return node.Unlocks;
                }
                Assert.Fail("machine upgrade research is not in the research node catalog");
                return null;
            }

            // 開いている機械の区画に選択レシピの束縛が載るまで待ち、継承元スロットの受け付けID数を返す
            // Wait until the open machine section carries the selected recipe's binding and return the source slot's accepted ID count
            async UniTask<int> SourceSlotAcceptedCount()
            {
                for (var i = 0; i < 50 && !HasSlotBinding(); i++) await UniTask.Delay(100);
                Assert.IsTrue(HasSlotBinding(), "slot bindings never arrived in the machine section after selecting the recipe");
                var acceptedItemIds = MachineSection().Detail.Binding.SlotBindings[0].AcceptedItemIds.Select(id => id.AsPrimitive()).ToArray();
                Debug.Log($"[MachineUpgradeSectionModelTest] machine section acceptedItemIds=[{string.Join(",", acceptedItemIds)}]");

                // 受け付けるのは継承元ファミリーの素材Lv1..3そのもの
                // The accepted IDs are exactly the source family's material Lv1..3
                var sourceLevelItemIds = new List<int>();
                foreach (var guid in new[] { "00000000-0000-0000-e0de-000000000011", "00000000-0000-0000-e0de-000000000012", "00000000-0000-0000-e0de-000000000013" })
                    sourceLevelItemIds.Add(MasterHolder.ItemMaster.GetItemId(Guid.Parse(guid)).AsPrimitive());
                CollectionAssert.AreEquivalent(sourceLevelItemIds, acceptedItemIds);
                return acceptedItemIds.Length;
            }

            bool HasSlotBinding()
            {
                return Resolver().Resolve<IMachineSectionModel>().Current.Value.TryGetPresent(out var machine) && 0 < machine.Detail.Binding.SlotBindings.Count;
            }

            #endregion
        }
    }
}
