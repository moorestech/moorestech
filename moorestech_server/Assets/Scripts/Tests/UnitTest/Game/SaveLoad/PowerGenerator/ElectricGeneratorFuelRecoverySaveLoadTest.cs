using System;
using System.Text.RegularExpressions;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.PowerGenerator;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.PowerGenerator
{
    public class ElectricGeneratorFuelRecoverySaveLoadTest
    {
        [SetUp]
        public void Setup() { Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil.CreateServer(); }

        [TestCase("Item")]
        [TestCase("Fluid")]
        public void FuelRemovedFromSettingsRejectsBeforeChangingExistingState(string kind)
        {
            var service = Create(out var param);
            var save = ValidSave(param, kind);
            service.Restore(save);
            var ticks = service.RemainingFuelTicks;
            if (kind == "Item") service._itemFuelSettings.Remove(MasterHolder.ItemMaster.GetItemId(param.FuelItems[0].ItemGuid));
            else service._fluidFuelSettings.Remove(MasterHolder.FluidMaster.GetFluidId(param.FuelFluids[0].FluidGuid));

            // 外部復元の拒否は既存タイマーも元の保存値も変更しない
            // Refusing external restoration changes neither the existing timer nor source values
            var original = Newtonsoft.Json.JsonConvert.SerializeObject(save);
            LogAssert.Expect(LogType.Error, new Regex("燃料の復元が不整合.*not configured"));
            Assert.Throws<InvalidOperationException>(() => service.Restore(save));
            Assert.AreEqual(ticks, service.RemainingFuelTicks);
            Assert.AreEqual(original, Newtonsoft.Json.JsonConvert.SerializeObject(save));
        }

        [TestCase("Item")]
        [TestCase("Fluid")]
        public void ConfiguredBurningFuelAndEmptyStateRoundTrip(string kind)
        {
            var service = Create(out var param);
            service.Restore(ValidSave(param, kind));
            var saved = new VanillaElectricGeneratorSaveJsonObject();
            service.WriteSaveData(saved);
            var loaded = Create(out _);
            loaded.Restore(saved);
            Assert.AreEqual(service.RemainingFuelTicks, loaded.RemainingFuelTicks);
            Assert.AreEqual(service.CurrentFuelTotalTicks, loaded.CurrentFuelTotalTicks);
            Assert.AreEqual(service.GetOutputEnergy(), loaded.GetOutputEnergy());
            Assert.IsTrue(loaded.IsBurning);
            loaded.Restore(new VanillaElectricGeneratorSaveJsonObject { ActiveFuelType = "None", RemainingFuelSeconds = 0 });
            Assert.IsFalse(loaded.IsBurning);
            Assert.AreEqual(0, loaded.RemainingFuelTicks);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(double.MaxValue)]
        public void InvalidBurningDurationIsRejected(double seconds)
        {
            var service = Create(out var param);
            var save = ValidSave(param, "Item");
            save.RemainingFuelSeconds = seconds;
            LogAssert.Expect(LogType.Error, new Regex("燃料の復元が不整合"));
            Assert.Throws<InvalidOperationException>(() => service.Restore(save));
            Assert.IsFalse(service.IsBurning);
            Assert.AreEqual(0, service.RemainingFuelTicks);
        }

        [TestCase("999", null)]
        [TestCase("Item", "not-a-guid")]
        [TestCase("None", "00000000-0000-0000-1234-000000000001")]
        public void InvalidKindOrIdentityIsRejected(string kind, string identity)
        {
            var service = Create(out _);
            var save = new VanillaElectricGeneratorSaveJsonObject { ActiveFuelType = kind, CurrentFuelItemGuidStr = identity };
            LogAssert.Expect(LogType.Error, new Regex("燃料の復元が不整合"));
            Assert.Throws<InvalidOperationException>(() => service.Restore(save));
            Assert.IsFalse(service.IsBurning);
        }

        private static VanillaElectricGeneratorFuelService Create(out ElectricGeneratorBlockParam param)
        {
            param = (ElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GeneratorId).BlockParam;
            // この境界試験では未配置の在庫から外部通知を出さない
            // This boundary test uses unplaced inventory without publishing external notifications
            var inventory = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, param.FuelItemSlotCount);
            return new VanillaElectricGeneratorFuelService(param, inventory);
        }

        private static VanillaElectricGeneratorSaveJsonObject ValidSave(ElectricGeneratorBlockParam param, string kind)
        {
            return new VanillaElectricGeneratorSaveJsonObject
            {
                ActiveFuelType = kind,
                CurrentFuelItemGuidStr = kind == "Item" ? param.FuelItems[0].ItemGuid.ToString() : null,
                CurrentFuelFluidGuidStr = kind == "Fluid" ? param.FuelFluids[0].FluidGuid.ToString() : null,
                RemainingFuelSeconds = 1,
            };
        }
    }
}
