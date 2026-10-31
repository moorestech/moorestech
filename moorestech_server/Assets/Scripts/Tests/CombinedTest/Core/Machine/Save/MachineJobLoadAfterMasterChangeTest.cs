using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.ItemsModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.CombinedTest.Core.Machine.Upgrade;
using Tests.CombinedTest.Game;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;

namespace Tests.CombinedTest.Core.Machine.Save
{
    /// <summary>
    ///     保存後にマスタや装備が変わった加工ジョブを、本番のロード前段とブロック復元・加工開始で扱えることを見る
    ///     Checks that jobs whose master or modules changed after fixing pass the production pre-load stage, block restore and job start
    /// </summary>
    public class MachineJobLoadAfterMasterChangeTest
    {
        private static readonly Vector3Int MachinePosition = new(3, 0, 3);
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
        public void WorldWithPrunedConsumedIdentityLoadsAndDropsTheJob()
        {
            var serviceProvider = SaveLoadPreparerTestFixture.CreateContainer();
            var (_, _, processor) = StartRunningJob();
            Assert.AreEqual(2, processor.GetSaveJsonObject().ConsumedInputItemGuids.Count);
            var save = JObject.Parse(serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson());

            // 消費実品の1つがマスタから消えたセーブを本番と同じ前段（移行連鎖＋欠損除去）へ通す
            // Pass a save whose consumed identity left the master through the production pre-load stage (migration chain + pruning)
            MachineProcessor(save)["consumedInputItemGuids"][0] = SaveLoadPreparerTestFixture.MissingGuid;
            var (reportStore, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            var prepared = preparer.Prepare(save.ToString());
            Assert.IsTrue(prepared.CanLoad, prepared.BlockedReason);
            Assert.AreEqual(1, reportStore.Report.EmptiedItemStackCount);
            Assert.AreEqual(JTokenType.Null, MachineProcessor(prepared.Save)["consumedInputItemGuids"][0].Type);

            // ワールド全体のロードを止めず、そのジョブだけを警告付きで破棄する
            // The world load continues and only that job is dropped with a warning
            LogAssert.Expect(LogType.Warning, new Regex("消費実品がマスタから除去されたためジョブを破棄します"));
            var loader = (WorldLoaderFromJson)SaveLoadPreparerTestFixture.CreateContainer().GetService<IWorldSaveDataLoader>();
            Assert.DoesNotThrow(() => loader.Load(prepared.Save));
            var restored = ServerContext.WorldBlockDatastore.GetBlock(MachinePosition).GetComponent<VanillaMachineProcessorComponent>();
            Assert.AreNotEqual(ProcessState.Processing, restored.CurrentState);
            Assert.IsNull(restored.GetSaveJsonObject().ConsumedInputItemGuids);
        }

        [Test]
        public void RunningJobSavedWithMoreConsumedInputsThanCurrentRecipeKeepsFixedTime()
        {
            SaveLoadPreparerTestFixture.CreateContainer();
            var (block, _, processor) = StartRunningJob();
            var before = processor.GetSaveJsonObject();

            // 保存時2入力→現行1入力と同じ「保存実品数＞現行入力数」のジョブを開く
            // Open a job that saved more consumed identities than the current recipe has inputs, as in a 2-to-1 input change
            var states = SaveLoadJsonTestHelper.ThroughJson(block.GetSaveState());
            var job = (JObject)((JObject)states[VanillaMachineSaveComponent.SaveKeyStatic])["processor"];
            ((JArray)job["consumedInputItemGuids"]).Add(Test1ItemGuid.ToString());
            LogAssert.Expect(LogType.Warning, new Regex("現行レシピと消費実品数が異なるため旧ジョブの返却方式で復元します"));
            var reloaded = ServerContext.BlockFactory.Load(block.BlockGuid, block.BlockInstanceId, states,
                new BlockPositionInfo(new Vector3Int(20, 0, 20), BlockDirection.North, Vector3Int.one));

            // 実品記録だけ旧方式へ戻し、加工中・確定時間・確定電力・残量は保つ
            // Only consumed identities fall back; the running state, fixed time, fixed power and remaining work are kept
            var restored = reloaded.GetComponent<VanillaMachineProcessorComponent>();
            var after = restored.GetSaveJsonObject();
            Assert.AreEqual(ProcessState.Processing, restored.CurrentState);
            Assert.IsNull(after.ConsumedInputItemGuids);
            Assert.AreEqual(before.TotalTicks, after.TotalTicks);
            Assert.AreEqual(before.PowerMultiplier, after.PowerMultiplier);
            Assert.AreEqual(before.RemainingSeconds, after.RemainingSeconds);
        }

        [Test]
        public void SpeedRealizationStartedAfterModuleRemovalKeepsFixedPower()
        {
            SaveLoadPreparerTestFixture.CreateContainer();
            var (block, inventory, processor) = PlaceMachine(MachinePosition);
            var recipe = LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var speed = MasterHolder.ItemMaster.Items.Modules.First(module => module.EffectAxis == ModuleMasterElement.EffectAxisConst.Speed);
            inventory.SetItem(ModuleRangeStart, MasterHolder.ItemMaster.GetItemId(speed.ItemGuid), 1);
            inventory.SetItem(InputSlotCount, CreateTest1(1));
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 2));
            inventory.InsertItem(CreateTest1(1));
            AdvanceTicksWithFullPower(1, processor);
            var fixedTicks = (uint)JObject.FromObject(processor.GetSaveJsonObject())["pendingRealization"]["totalTicks"];
            Assert.Less(fixedTicks, GameUpdater.SecondsToTicks(recipe.Time), "速度モジュールで時間が短縮されていない");

            // 出口待ちの間に速度モジュールを外し、1tick以上経ってから出口を空ける
            // Remove the speed module while the output is blocked, then free the output after more than one tick
            inventory.SetItem(ModuleRangeStart, ServerContext.ItemStackFactory.CreatEmpty());
            AdvanceTicksWithFullPower(2, processor);
            Assert.AreNotEqual(ProcessState.Processing, processor.CurrentState);
            inventory.SetItem(InputSlotCount, ServerContext.ItemStackFactory.CreatEmpty());
            AdvanceTicksWithFullPower(1, processor);

            // 短縮時間と同時に確定した電力倍率で加工する
            // Processing uses the power multiplier fixed together with the shortened time
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(fixedTicks, processor.GetSaveJsonObject().TotalTicks);
            Assert.AreEqual(1f + speed.TradeoffValue, processor.GetSaveJsonObject().PowerMultiplier);
            Assert.AreEqual(processor.RequestPower * (1f + speed.TradeoffValue), processor.EffectiveRequestPower, 0.0001f);
        }

        private static (IBlock block, VanillaMachineBlockInventoryComponent inventory, VanillaMachineProcessorComponent processor) StartRunningJob()
        {
            var (block, inventory, processor) = PlaceMachine(MachinePosition);
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid));
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 2));
            inventory.InsertItem(CreateTest1(1));
            AdvanceTicksWithFullPower(1, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            return (block, inventory, processor);
        }

        private static JObject MachineProcessor(JObject save)
        {
            var machine = ((JArray)save["world"]).OfType<JObject>().Single(block => block["state"]?[VanillaMachineSaveComponent.SaveKeyStatic] != null);
            return (JObject)machine["state"][VanillaMachineSaveComponent.SaveKeyStatic]["processor"];
        }
    }
}
