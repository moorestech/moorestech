using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.ItemsModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.Machine.Upgrade
{
    /// <summary>
    ///     品質モジュールの最高 tier と効果合計が機械から取り出せることを検証する
    ///     Verifies the highest quality-module tier and the summed quality effect are readable from a machine
    /// </summary>
    public class MachineModuleQualityTierTest
    {
        // TestElectricMachine は品質可・入力2・出力3・モジュール4
        // TestElectricMachine allows quality and has 2 inputs, 3 outputs and 4 module slots
        private const int ModuleRangeStart = 5;

        [Test]
        public void AggregateReportsHighestQualityTierTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var tier1 = QualityModuleOfTier(1);
            var tier2 = QualityModuleOfTier(2);
            var speed = MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Speed);

            // tier1×1 と tier2×2 なら最高は2、合計は個数加重
            // tier1 x1 plus tier2 x2 gives highest 2 and a count-weighted sum
            var effect = MachineModuleEffect.Aggregate(new List<MachineModuleEffect.EquippedModule> { new(tier1, 1), new(tier2, 2) });
            Assert.AreEqual(2, effect.HighestQualityModuleTier);
            Assert.AreEqual(tier1.EffectValue + tier2.EffectValue * 2, effect.QualityShift, 0.0001f);

            // 品質モジュールが無ければ0
            // Zero without any quality module
            var speedOnly = MachineModuleEffect.Aggregate(new List<MachineModuleEffect.EquippedModule> { new(speed, 1) });
            Assert.AreEqual(0, speedOnly.HighestQualityModuleTier);
        }

        [Test]
        public void MachineComponentExposesHighestQualityTierTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 1, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            inventory.SetItem(ModuleRangeStart, ServerContext.ItemStackFactory.Create(QualityModuleOfTier(1).ItemGuid, 1));
            inventory.SetItem(ModuleRangeStart + 1, ServerContext.ItemStackFactory.Create(QualityModuleOfTier(2).ItemGuid, 1));

            var effect = block.GetComponent<MachineModuleEffectComponent>().AggregateCurrent();
            Assert.AreEqual(2, effect.HighestQualityModuleTier);
        }

        private static ModuleMasterElement QualityModuleOfTier(int tier)
        {
            return MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Quality && m.Tier == tier);
        }
    }
}
