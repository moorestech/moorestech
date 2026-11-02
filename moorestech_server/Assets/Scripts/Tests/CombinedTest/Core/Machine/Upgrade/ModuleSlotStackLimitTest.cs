using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using Game.PlayerIdentity;
using Game.PlayerInventory.Interface;
using Game.Research;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.ItemsModule;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Tests.Module.TestMod;
using Tests.Util.PlayerIdentity;
using UnityEngine;

namespace Tests.CombinedTest.Core.Machine.Upgrade
{
    /// <summary>
    ///     モジュールスロットのスタック上限（移動の頭打ち・入れ替えの拒否・効果の頭打ち・研究での引き上げと復元）を検証する
    ///     Verifies the module-slot stack limit: capped moves, refused swaps, capped effects, and research raising and restoring it
    /// </summary>
    public class ModuleSlotStackLimitTest
    {
        // TestElectricMachine は入力2・出力3・モジュール4
        // TestElectricMachine has 2 inputs, 3 outputs and 4 module slots
        private const int ModuleRangeStart = 5;

        [Test]
        public void MoveIntoModuleSlotStopsAtStackLimitTest()
        {
            var (serviceProvider, _, mainInventory, block) = Setup();
            var machineInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(3, serviceProvider.GetService<IModuleSlotStackLimitLookup>().CurrentStackLimit);
            mainInventory.SetItem(0, CreateModule(ModuleMasterElement.EffectAxisConst.Speed, 5));

            // 上限3なので3個だけ入り、2個はプレイヤー側に戻る
            // With a limit of 3 only 3 go in and 2 return to the player
            Assert.AreEqual(InventoryItemMoveResult.Moved, InventoryItemMoveService.Move(mainInventory, 0, machineInventory, ModuleRangeStart, 5));
            Assert.AreEqual(3, machineInventory.GetItem(ModuleRangeStart).Count);
            Assert.AreEqual(2, mainInventory.GetItem(0).Count);

            // 満杯のスロットへは1個も入らず、全部戻る
            // A full slot takes none and everything returns
            InventoryItemMoveService.Move(mainInventory, 0, machineInventory, ModuleRangeStart, 2);
            Assert.AreEqual(3, machineInventory.GetItem(ModuleRangeStart).Count);
            Assert.AreEqual(2, mainInventory.GetItem(0).Count);
        }

        [Test]
        public void ResearchRaisesStackLimitTest()
        {
            var (serviceProvider, playerId, mainInventory, block) = Setup();
            var machineInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();

            // 研究でレベル1（上限6）になる
            // The research raises it to level 1 (limit 6)
            Assert.IsTrue(serviceProvider.GetService<IResearchDataStore>().CompleteResearch(ForUnitTestLevelInheritanceIds.ModuleSlotStackResearchGuid, playerId));
            Assert.AreEqual(6, serviceProvider.GetService<IModuleSlotStackLimitLookup>().CurrentStackLimit);

            mainInventory.SetItem(0, CreateModule(ModuleMasterElement.EffectAxisConst.Speed, 5));
            InventoryItemMoveService.Move(mainInventory, 0, machineInventory, ModuleRangeStart, 5);
            Assert.AreEqual(5, machineInventory.GetItem(ModuleRangeStart).Count);
            Assert.AreEqual(ItemMaster.EmptyItemId, mainInventory.GetItem(0).Id);

            // 段階表の外のレベル（テストマスタは 0..1）は丸めずに止める
            // A level outside the table (0..1 in the test master) stops instead of clamping
            var json = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "research.json")));
            var node = json["data"].Single(value => value["researchNodeGuid"].Value<string>() == ForUnitTestLevelInheritanceIds.ModuleSlotStackResearchGuid.ToString());
            node["clearedActions"][0]["gameActionParam"]["level"] = 2;
            var researchMaster = new ResearchMaster(json);
            researchMaster.Initialize();
            var action = researchMaster.GetResearch(ForUnitTestLevelInheritanceIds.ModuleSlotStackResearchGuid).ClearedActions.items[0];
            Assert.Throws<ArgumentOutOfRangeException>(() => serviceProvider.GetService<IModuleSlotStackLimitUnlocker>().ApplyUnlockModuleSlotStackLevelAction(action));
        }

        [Test]
        public void SwapAboveStackLimitIsRejectedTest()
        {
            var (_, _, mainInventory, block) = Setup();
            var machineInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var efficiency = CreateModule(ModuleMasterElement.EffectAxisConst.Efficiency, 1);
            machineInventory.SetItem(ModuleRangeStart, efficiency);

            // 入れ替えは丸ごと置くため、上限を超える5個は拒否される
            // A swap writes the whole stack, so 5 above the limit is refused
            mainInventory.SetItem(0, CreateModule(ModuleMasterElement.EffectAxisConst.Speed, 5));
            Assert.AreEqual(MachineSlotPlacementCheck.ExceedsModuleSlotStackLimit, machineInventory.CheckPlacement(ModuleRangeStart, mainInventory.GetItem(0)));
            Assert.AreEqual(InventoryItemMoveResult.RejectedByDestination, InventoryItemMoveService.Move(mainInventory, 0, machineInventory, ModuleRangeStart, 5));
            Assert.AreEqual(efficiency.Id, machineInventory.GetItem(ModuleRangeStart).Id);

            // 上限以内なら入れ替わる
            // Within the limit the swap goes through
            var speed = CreateModule(ModuleMasterElement.EffectAxisConst.Speed, 3);
            mainInventory.SetItem(0, speed);
            Assert.AreEqual(InventoryItemMoveResult.Moved, InventoryItemMoveService.Move(mainInventory, 0, machineInventory, ModuleRangeStart, 3));
            Assert.AreEqual(speed.Id, machineInventory.GetItem(ModuleRangeStart).Id);
            Assert.AreEqual(efficiency.Id, mainInventory.GetItem(0).Id);
        }

        [Test]
        public void EffectCountsOnlyUpToStackLimitTest()
        {
            var (_, _, _, block) = Setup();

            // 判定を通さずに5個書いても、効果は上限の3個分
            // Even with 5 written past the check, the effect counts only the limit of 3
            block.GetComponent<VanillaMachineBlockInventoryComponent>().SetItem(ModuleRangeStart, CreateModule(ModuleMasterElement.EffectAxisConst.Speed, 5));
            var speed = MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Speed);
            var expected = MachineModuleEffect.Aggregate(new List<MachineModuleEffect.EquippedModule> { new(speed, 3) });

            var actual = block.GetComponent<MachineModuleEffectComponent>().AggregateCurrent();
            Assert.AreEqual(expected.ProcessingTimeMultiplier, actual.ProcessingTimeMultiplier, 0.0001f);
            Assert.AreEqual(expected.PowerMultiplier, actual.PowerMultiplier, 0.0001f);
        }

        [Test]
        public void StackLimitIsRestoredFromResearchOnLoadTest()
        {
            var (serviceProvider, playerId, _, block) = Setup();
            serviceProvider.GetService<IResearchDataStore>().CompleteResearch(ForUnitTestLevelInheritanceIds.ModuleSlotStackResearchGuid, playerId);
            block.GetComponent<VanillaMachineBlockInventoryComponent>().SetItem(ModuleRangeStart, CreateModule(ModuleMasterElement.EffectAxisConst.Speed, 5));
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 新しいコンテナでロードすると、研究の再実行で上限6が戻り、5個がそのまま残る
            // Loading into a fresh container replays the research, restoring limit 6 and keeping the 5 modules
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);
            Assert.AreEqual(6, loadServiceProvider.GetService<IModuleSlotStackLimitLookup>().CurrentStackLimit);
            var loadedInventory = ServerContext.WorldBlockDatastore.GetBlock<VanillaMachineBlockInventoryComponent>(new Vector3Int(1, 1, 1));
            Assert.AreEqual(5, loadedInventory.GetItem(ModuleRangeStart).Count);
        }

        private static (ServiceProvider serviceProvider, int playerId, IOpenableInventory mainInventory, IBlock block) Setup()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(serviceProvider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 1, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            return (serviceProvider, playerId, mainInventory, block);
        }

        private static IItemStack CreateModule(string effectAxis, int count)
        {
            var module = MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == effectAxis);
            return ServerContext.ItemStackFactory.Create(module.ItemGuid, count);
        }
    }
}
