using System;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.ItemsModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.Machine.Upgrade
{
    /// <summary>
    ///     品質モジュールが allowQualityModule の機械にだけ置けて効くことを検証する
    ///     Verifies quality modules are placeable and effective only on machines with allowQualityModule
    /// </summary>
    public class MachineModuleQualityRestrictionTest
    {
        // TestElectricMachine（許可）と TestGearMachine（不許可）はどちらも入力2・出力3・モジュール4
        // TestElectricMachine (allowed) and TestGearMachine (disallowed) both have 2 inputs, 3 outputs and 4 module slots
        private const int ModuleRangeStart = 5;
        private const int PlayerId = 1;

        [Test]
        public void QualityModulePlacementFollowsMachinePermissionTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var gearInventory = PlaceInventory(ForUnitTestModBlockId.GearMachine, new Vector3Int(1, 1, 1));
            var electricInventory = PlaceInventory(ForUnitTestModBlockId.MachineId, new Vector3Int(5, 1, 1));

            // 不許可機械は品質だけを拒否し、速度と非モジュールは従来どおり置ける
            // A disallowed machine refuses only quality; speed and non-module items stay placeable as before
            Assert.AreEqual(MachineSlotPlacementCheck.ModuleNotAllowedOnMachine, gearInventory.CheckPlacement(ModuleRangeStart, CreateModule(ModuleMasterElement.EffectAxisConst.Quality)));
            Assert.AreEqual(MachineSlotPlacementCheck.Allowed, gearInventory.CheckPlacement(ModuleRangeStart, CreateModule(ModuleMasterElement.EffectAxisConst.Speed)));
            Assert.AreEqual(MachineSlotPlacementCheck.Allowed, gearInventory.CheckPlacement(ModuleRangeStart, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId4, 1)));

            // 許可機械は品質を受け入れる
            // An allowed machine accepts quality
            Assert.AreEqual(MachineSlotPlacementCheck.Allowed, electricInventory.CheckPlacement(ModuleRangeStart, CreateModule(ModuleMasterElement.EffectAxisConst.Quality)));
        }

        [Test]
        public void QualityModuleMoveIntoDisallowedMachineIsRejectedTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var gearInventory = PlaceInventory(ForUnitTestModBlockId.GearMachine, new Vector3Int(1, 1, 1));
            var qualityModule = CreateModule(ModuleMasterElement.EffectAxisConst.Quality);
            mainInventory.SetItem(0, qualityModule);

            // 移動は拒否され、モジュールはプレイヤー側に残る
            // The move is refused and the module stays with the player
            var result = InventoryItemMoveService.Move(mainInventory, 0, gearInventory, ModuleRangeStart, 1);
            Assert.AreEqual(InventoryItemMoveResult.RejectedByDestination, result);
            Assert.AreEqual(qualityModule.Id, mainInventory.GetItem(0).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, gearInventory.GetItem(ModuleRangeStart).Id);
        }

        [Test]
        public void DisallowedQualityModuleIsExcludedFromEffectTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMachine, new Vector3Int(1, 1, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();

            // セーブ由来を模して判定を通さずに書き込んでも、効果にもトレードオフにも数えない
            // Even when written without the check (as a save would), it counts toward neither the effect nor its tradeoff
            inventory.SetItem(ModuleRangeStart, CreateModule(ModuleMasterElement.EffectAxisConst.Quality));
            var effect = block.GetComponent<MachineModuleEffectComponent>().AggregateCurrent();
            Assert.AreEqual(0f, effect.QualityShift, 0.0001f);
            Assert.AreEqual(1f, effect.ProcessingTimeMultiplier, 0.0001f);
        }

        private static VanillaMachineBlockInventoryComponent PlaceInventory(BlockId blockId, Vector3Int position)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            return block.GetComponent<VanillaMachineBlockInventoryComponent>();
        }

        private static IItemStack CreateModule(string effectAxis)
        {
            var module = MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == effectAxis);
            return ServerContext.ItemStackFactory.Create(module.ItemGuid, 1);
        }
    }
}
