using System;
using System.Text.RegularExpressions;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.PowerGenerator;
using Game.Context;
using Game.Fluid;
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
        public void FuelRemovedFromSettingsPreservesLifetimeAndIdentity(string kind)
        {
            var service = Create(out var param);
            var save = ValidSave(param, kind);
            service.Restore(save);
            var ticks = service.RemainingFuelTicks;
            if (kind == "Item") service._itemFuelSettings.Remove(MasterHolder.ItemMaster.GetItemId(param.FuelItems[0].ItemGuid));
            else service._fluidFuelSettings.Remove(MasterHolder.FluidMaster.GetFluidId(param.FuelFluids[0].FluidGuid));

            // 許可除去後も寿命とGUIDを再保存
            // Preserve consumed lifetime and its GUID when current ignition permissions change
            var original = Newtonsoft.Json.JsonConvert.SerializeObject(save);
            LogAssert.Expect(LogType.Warning, new Regex("許可設定から外れた既消費燃料"));
            service.Restore(save);
            Assert.AreEqual(0, service.GetOutputEnergy().AsPrimitive());
            var saved = new VanillaElectricGeneratorSaveJsonObject();
            service.WriteSaveData(saved);
            Assert.AreEqual(save.CurrentFuelItemGuidStr, saved.CurrentFuelItemGuidStr);
            Assert.AreEqual(save.CurrentFuelFluidGuidStr, saved.CurrentFuelFluidGuidStr);
            LogAssert.Expect(LogType.Warning, new Regex("許可設定から外れた既消費燃料"));
            service.Restore(saved);
            Assert.AreEqual(ticks, service.RemainingFuelTicks);
            Assert.AreEqual(original, Newtonsoft.Json.JsonConvert.SerializeObject(save));
            service.Update();
            Assert.AreEqual(ticks - 1, service.RemainingFuelTicks);
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

        [TestCase(0)]
        [TestCase(1)]
        public void NoFluidFuelRejectsInputAndPreservesSavedResidualTest(double residual)
        {
            var service = Create(out var param);
            var fluidGuid = param.FuelFluids[0].FluidGuid;
            var fluidId = MasterHolder.FluidMaster.GetFluidId(fluidGuid);
            service._fluidFuelSettings.Clear();
            service.Restore(new VanillaElectricGeneratorSaveJsonObject
            {
                ActiveFuelType = "None",
                FluidTank = new VanillaElectricGeneratorFluidSaveJsonObject { FluidGuidStr = fluidGuid.ToString(), Amount = residual },
            });

            // 新規搬入は全量戻し、保存済み残液は保持する
            // Return all new input while preserving saved residual fluid
            var remainder = service.AddLiquid(new FluidStack(1, fluidId));
            Assert.AreEqual(1, remainder.Amount);
            Assert.AreEqual(fluidId, remainder.FluidId);
            var saved = new VanillaElectricGeneratorSaveJsonObject();
            service.WriteSaveData(saved);
            Assert.AreEqual(residual, saved.FluidTank.Amount);
            Assert.AreEqual(fluidGuid.ToString(), saved.FluidTank.FluidGuidStr);
        }

        [TestCase("999", null)]
        [TestCase("Item", "not-a-guid")]
        [TestCase("Item", null)]
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
