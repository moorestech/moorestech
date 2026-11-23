using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.ItemsModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.Machine.Upgrade
{
    /// <summary>
    ///     追加装着可否と上限で切った有効装着一覧を検証する
    ///     Verifies additional module capacity and the limit-clipped effective loadout
    /// </summary>
    public class MachineModuleEquipOneMoreTest
    {
        private const int ModuleRangeStart = 5;
        private const int ModuleSlotCount = 4;

        [Test]
        public void AcceptsOneMoreOnlyWhenEffectiveSpaceRemainsTest()
        {
            var (inventory, effectComponent, limit) = Setup();
            var tier1 = QualityModuleOfTier(1);
            var tier2 = QualityModuleOfTier(2);

            // 空き枠なら別 tier も入る
            // An empty slot accepts another tier
            Assert.IsTrue(effectComponent.CanEquipOneMore(tier2));

            // 同じモジュールの枠に上限未満の空きがあれば入る
            // The same module fits while its stack is below the limit
            FillModuleSlots(inventory, tier1, limit - 1);
            Assert.IsTrue(effectComponent.CanEquipOneMore(tier1));
            Assert.IsFalse(effectComponent.CanEquipOneMore(tier2));

            // 全枠が上限なら追加できない
            // No module fits when every slot is at its limit
            FillModuleSlots(inventory, tier1, limit);
            Assert.IsFalse(effectComponent.CanEquipOneMore(tier1));
        }

        [Test]
        public void CanEquipMatchesInsertionWhenItemStackFillsBeforeSlotLimitTest()
        {
            var (inventory, effects, limit) = Setup();
            var module = QualityModuleOfTier(1);
            var item = MasterHolder.ItemMaster.GetItemMaster(module.ItemGuid);
            var counts = MasterHolder.ItemMaster.GetStackLevelTable(item.StackLevelTableGuid).StackCounts;

            // このテストのロード済みマスタにだけ、機械上限より小さいアイテム上限を設定する
            // Use an item limit below the machine limit in this test's loaded master only
            var originalCount = counts[0];
            try
            {
                counts[0] = limit - 1;
                FillModuleSlots(inventory, module, counts[0]);
                var slots = Enumerable.Range(ModuleRangeStart, ModuleSlotCount).ToArray();
                var before = slots.Select(inventory.GetItem).ToArray();
                var canEquip = effects.CanEquipOneMore(module);
                foreach (var slot in slots) Assert.AreSame(before[slot - ModuleRangeStart], inventory.GetItem(slot));

                // 全枠で実挿入も試し、1個すべてが戻ることと照会結果を突き合わせる
                // Compare the prediction against every real insertion returning the entire item
                var incoming = ServerContext.ItemStackFactory.Create(module.ItemGuid, 1);
                foreach (var slot in slots) Assert.AreEqual(1, inventory.ReplaceItem(slot, incoming).Count);
                Assert.IsFalse(canEquip, "All insertions returned the item, yet the query reported space.");
            }
            finally
            {
                counts[0] = originalCount;
            }
        }

        [Test]
        public void CollectEffectiveModulesClipsEachStackToLimitTest()
        {
            var (inventory, effectComponent, limit) = Setup();
            var tier1 = QualityModuleOfTier(1);
            inventory.SetItem(ModuleRangeStart, ServerContext.ItemStackFactory.Create(tier1.ItemGuid, limit + 2));

            var modules = effectComponent.CollectEffectiveModules();
            Assert.AreEqual(1, modules.Count);
            Assert.AreEqual(tier1, modules[0].Module);
            Assert.AreEqual(limit, modules[0].Count);
        }

        private static (VanillaMachineBlockInventoryComponent inventory, MachineModuleEffectComponent effectComponent, int limit) Setup()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 1, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var limit = serviceProvider.GetService<IModuleSlotStackLimitLookup>().CurrentStackLimit;
            return (block.GetComponent<VanillaMachineBlockInventoryComponent>(), block.GetComponent<MachineModuleEffectComponent>(), limit);
        }

        private static ModuleMasterElement QualityModuleOfTier(int tier)
        {
            return MasterHolder.ItemMaster.Items.Modules.First(module => module.EffectAxis == ModuleMasterElement.EffectAxisConst.Quality && module.Tier == tier);
        }

        private static void FillModuleSlots(VanillaMachineBlockInventoryComponent inventory, ModuleMasterElement module, int count)
        {
            for (var slot = 0; slot < ModuleSlotCount; slot++)
                inventory.SetItem(ModuleRangeStart + slot, ServerContext.ItemStackFactory.Create(module.ItemGuid, count));
        }
    }
}
