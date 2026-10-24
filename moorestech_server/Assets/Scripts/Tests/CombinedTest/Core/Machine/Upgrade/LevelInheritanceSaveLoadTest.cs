using System;
using System.IO;
using System.Text.RegularExpressions;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Game;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;

namespace Tests.CombinedTest.Core.Machine.Upgrade
{
    /// <summary>
    ///     加工中ジョブの継承レベルがセーブ・ロードで保たれ、欠けたセーブは読まずに止まり、レシピが消えたジョブは継承レベルごと捨てて待機へ戻り、版6の通常機械は継承レベル無しで読めることを検証する
    ///     Verifies the running job's inherited level survives save/load, a save missing it stops loading, a job whose recipe vanished is dropped with its level and falls back to idle, and a version-6 plain machine loads with no inherited level
    /// </summary>
    public class LevelInheritanceSaveLoadTest
    {
        private static readonly Vector3Int MachinePosition = new(1, 1, 1);
        private const string VanillaKey = "Game.Block.Blocks.Machine.VanillaMachineSaveComponent";
        private string _archiveRoot;

        [SetUp]
        public void SetUp()
        {
            _archiveRoot = SaveLoadPreparerTestFixture.ArchiveRootForThisRun();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_archiveRoot)) Directory.Delete(_archiveRoot, true);
        }

        [Test]
        public void InheritedLevelSurvivesSaveLoadTest()
        {
            var saveJson = BuildInheritanceProcessingSave().ToString();

            // 別コンテナへロードし、選択解除の返却が Lv3 であることを見る（返却は入力スロット→溢れ先の順なので両方を数える）
            // Load into a fresh container and check the clear-selection refund is Lv3 (refunds go to input slots first, then overflow, so count both)
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);
            var loadedInventory = ServerContext.WorldBlockDatastore.GetBlock(MachinePosition).GetComponent<VanillaMachineBlockInventoryComponent>();
            var overflow = CreateOverflow(10);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, GetProcessor().ClearSelectedRecipe(overflow));
            Assert.AreEqual(2, CountItem(loadedInventory, 0, InputSlotCount, ForUnitTestLevelInheritanceIds.Source(3)) + CountItem(overflow, 0, 10, ForUnitTestLevelInheritanceIds.Source(3)));
            Assert.AreEqual(0, CountItem(loadedInventory, 0, InputSlotCount, ForUnitTestLevelInheritanceIds.Source(1)) + CountItem(overflow, 0, 10, ForUnitTestLevelInheritanceIds.Source(1)));
        }

        [Test]
        public void InheritanceJobWithoutLevelStopsLoadingTest()
        {
            // 継承レシピの加工中ジョブから継承レベルを消すと、Lv1 で補わずロードが止まる
            // Stripping the level from an inheritance job stops loading instead of filling in Lv1
            var save = BuildInheritanceProcessingSave();
            Assert.IsTrue(ProcessorJson(save).Remove("inheritedLevel"));
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var loader = loadProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            var exception = Assert.Catch<Exception>(() => loader.Load(save.ToString()));
            StringAssert.Contains("継承レベル", exception.ToString());
        }

        [Test]
        public void InheritanceJobWithMissingRecipeFallsBackToIdleTest()
        {
            // 継承レベルつきのジョブのレシピGUIDをマスタに無い値へ替えると、例外で止めずにジョブを捨てて待機へ戻る
            // Swapping the job's recipe GUID for one the master lacks drops the job instead of throwing and falls back to idle
            var save = BuildInheritanceProcessingSave();
            ProcessorJson(save)["recipeGuid"] = "00000000-0000-4000-8000-0000deadbeef";
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            LogAssert.Expect(LogType.Warning, new Regex("加工中レシピがマスタにない"));
            (loadProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(save.ToString());

            // 待機に戻り、保存し直すと継承レベルも残らない
            // It is idle, and re-saving keeps no inherited level either
            Assert.AreEqual(ProcessState.Idle, GetProcessor().CurrentState);
            var resaved = JObject.Parse(loadProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
            Assert.AreEqual(JTokenType.Null, ProcessorJson(resaved)["inheritedLevel"].Type);
        }

        [Test]
        public void Version6PlainMachineLoadsWithoutInheritedLevelTest()
        {
            // 通常レシピで加工中の機械から継承レベルを消して版6と名乗らせ、ファイル経由でマイグレーション連鎖を通す
            // Strip the level from a plain-recipe job, label it version 6 and load from a file so the migration chain runs
            var save = BuildPlainProcessingSave();
            Assert.IsTrue(ProcessorJson(save).Remove("inheritedLevel"));
            save["worldVersion"] = 6;
            Directory.CreateDirectory(_archiveRoot);
            var sourcePath = WorldDataDirectory.FromWorldRoot(_archiveRoot).SaveJsonFilePath;
            File.WriteAllText(sourcePath, save.ToString());
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, sourcePath),
            };
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(options);
            loadProvider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();

            // 加工中のまま読め、保存し直すと継承レベルは null（版6に継承レシピは無かった）
            // It loads still processing and re-saves with a null level (version 6 had no inheritance recipes)
            Assert.AreEqual(ProcessState.Processing, GetProcessor().CurrentState);
            var resaved = JObject.Parse(loadProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
            Assert.AreEqual(JTokenType.Null, ProcessorJson(resaved)["inheritedLevel"].Type);

            #region Internal

            // 通常レシピ（テストマスタの先頭）で加工中の通常機械を持つ現行セーブを作る
            // Build a current save holding a plain machine mid-job on the plain recipe (the first test recipe)
            static JObject BuildPlainProcessingSave()
            {
                var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
                var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
                ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, MachinePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
                MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
                var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
                foreach (var input in recipe.InputItems) inventory.InsertItem(ServerContext.ItemStackFactory.Create(input.ItemGuid, input.Count));
                var processor = block.GetComponent<VanillaMachineProcessorComponent>();
                AdvanceTicksWithFullPower(2, processor);
                Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

                var save = JObject.Parse(provider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
                Assert.AreEqual(JTokenType.Null, ProcessorJson(save)["inheritedLevel"].Type);
                return save;
            }

            #endregion
        }

        // Lv3 で加工中の継承機械を持つ現行セーブを作る
        // Build a current save holding the inheritance machine mid-job at Lv3
        private static JObject BuildInheritanceProcessingSave()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, processor) = PlaceMachine(MachinePosition);
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.InheritRecipeGuid));
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(3), 2));
            inventory.InsertItem(CreateTest1(1));
            AdvanceTicksWithFullPower(2, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            var save = JObject.Parse(provider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
            Assert.AreEqual(3, ProcessorJson(save)["inheritedLevel"].Value<int>());
            return save;
        }

        private static VanillaMachineProcessorComponent GetProcessor()
        {
            return ServerContext.WorldBlockDatastore.GetBlock(MachinePosition).GetComponent<VanillaMachineProcessorComponent>();
        }

        private static JObject ProcessorJson(JObject save)
        {
            foreach (var block in (JArray)save["world"])
            {
                if (block["X"].Value<int>() == MachinePosition.x && block["Y"].Value<int>() == MachinePosition.y && block["Z"].Value<int>() == MachinePosition.z) return (JObject)block["state"][VanillaKey]["processor"];
            }
            Assert.Fail($"セーブに {MachinePosition} のブロックがありません");
            return null;
        }
    }
}
