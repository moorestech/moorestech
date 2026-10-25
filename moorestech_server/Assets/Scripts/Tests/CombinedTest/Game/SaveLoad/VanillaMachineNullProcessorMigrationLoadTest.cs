using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.MachineRecipesModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;

namespace Tests.CombinedTest.Game.SaveLoad
{
    // 版7の通常機械で processor だけ null のセーブが、移行後のロードで加工状態の復元だけ見送りインベントリを保つことを検証
    // Verify a version-7 plain machine whose processor alone is null loads after migration, skipping only the processing state and keeping its inventories
    public class VanillaMachineNullProcessorMigrationLoadTest
    {
        private static readonly Vector3Int MachinePosition = new(1, 1, 1);
        private const string VanillaKey = "Game.Block.Blocks.Machine.VanillaMachineSaveComponent";

        // テスト用機械のスロット構成（blocks.json の TestElectricMachine に対応）
        // Slot layout of the test machine (matches TestElectricMachine in blocks.json)
        private const int InputSlotCount = 2;
        private const int OutputSlotCount = 3;

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
        public void 版7の通常機械のprocessorがnullでもインベントリを保ってロードできるTest()
        {
            // 入力・出力・モジュールを持つ正常セーブから processor だけ null にして版7と名乗らせる
            // Null only the processor of a normal save holding input, output and module items, then label it version 7
            var save = BuildMachineSave();
            var originalState = (JObject)MachineState(save).DeepClone();
            MachineState(save)["processor"] = JValue.CreateNull();
            save["worldVersion"] = 7;

            // 一時ファイルを実セーブパスに指定し、本番と同じ入口でロードする
            // Point the DI save path at a temporary file and load through the production entry point
            Directory.CreateDirectory(_archiveRoot);
            var sourcePath = WorldDataDirectory.FromWorldRoot(_archiveRoot).SaveJsonFilePath;
            File.WriteAllText(sourcePath, save.ToString());
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, sourcePath),
            };
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(options);
            LogAssert.Expect(LogType.Warning, new Regex("processor が null"));
            loadProvider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();

            // 加工状態は待機・未選択で始まり、インベントリは原本のまま残る
            // The processing state starts idle and unselected, and every inventory survives as saved
            var block = ServerContext.WorldBlockDatastore.GetBlock(MachinePosition);
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            var resaved = JObject.Parse(loadProvider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, resaved["worldVersion"].Value<int>());
            foreach (var slotKey in new[] { "inputSlot", "outputSlot", "moduleSlot" })
            {
                Assert.IsTrue(JToken.DeepEquals(originalState[slotKey], MachineState(resaved)[slotKey]), $"{slotKey} がロードで変わりました: {MachineState(resaved)[slotKey]}");
            }

            // 機械は止まらず、レシピを選び直せば残った入力で加工を始める
            // The machine keeps working: reselecting the recipe starts processing from the kept inputs
            MachineRecipeSelectTestUtil.SelectRecipe(block, GetMachineRecipe());
            AdvanceTicksWithFullPower(2, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            #region Internal

            // レシピ入力・出力品・モジュールを入れ、加工前の通常機械を持つ現行セーブを作る
            // Build a current save holding an unstarted plain machine with recipe inputs, an output item and a module
            static JObject BuildMachineSave()
            {
                var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
                ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, MachinePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
                var recipe = GetMachineRecipe();
                MachineRecipeSelectTestUtil.SelectRecipe(machine, recipe);
                var inventory = machine.GetComponent<VanillaMachineBlockInventoryComponent>();
                foreach (var input in recipe.InputItems) inventory.InsertItem(ServerContext.ItemStackFactory.Create(input.ItemGuid, input.Count));
                inventory.SetItem(InputSlotCount, ServerContext.ItemStackFactory.Create(recipe.OutputItems[0].ItemGuid, 7));
                var moduleItemId = MasterHolder.ItemMaster.GetItemId(MasterHolder.ItemMaster.Items.Modules.First().ItemGuid);
                inventory.SetItem(InputSlotCount + OutputSlotCount, ServerContext.ItemStackFactory.Create(moduleItemId, 1));

                var built = JObject.Parse(provider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
                Assert.AreEqual(JTokenType.Object, MachineState(built)["processor"].Type);
                return built;
            }

            #endregion
        }

        private static MachineRecipeMasterElement GetMachineRecipe()
        {
            var machineBlockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            return MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == machineBlockGuid);
        }

        private static JObject MachineState(JObject save)
        {
            foreach (var block in (JArray)save["world"])
            {
                if (block["X"].Value<int>() == MachinePosition.x && block["Y"].Value<int>() == MachinePosition.y && block["Z"].Value<int>() == MachinePosition.z) return (JObject)block["state"][VanillaKey];
            }
            Assert.Fail($"セーブに {MachinePosition} のブロックがありません");
            return null;
        }
    }
}
