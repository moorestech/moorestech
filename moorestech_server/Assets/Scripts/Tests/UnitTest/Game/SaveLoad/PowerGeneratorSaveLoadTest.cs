using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class PowerGeneratorSaveLoadTest
    {
        
        [Test]
        public void PowerGeneratorTest()
        {
            // テスト用の依存関係を初期化し、必要なサービスを取得する
            _ = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // テスト対象の発電機ブロックを生成する
            var generatorMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GeneratorId);
            var fuelSlotCount = (generatorMaster.BlockParam as ElectricGeneratorBlockParam).FuelItemSlotCount;
            var generatorPosInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var powerGeneratorBlock = blockFactory.Create(ForUnitTestModBlockId.GeneratorId, new BlockInstanceId(10), generatorPosInfo);
            var powerGenerator = powerGeneratorBlock.GetComponent<VanillaElectricGeneratorComponent>();

            // 検証したい燃料・液体状態をマスターデータから取得する
            var fuelItemId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));
            var secondaryItemId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000002"));
            var fuelFluidId = MasterHolder.FluidMaster.GetFluidId(Guid.Parse("00000000-0000-0000-1234-000000000003"));
            const double remainingFuelTime = 567d;

            // プライベートフィールドを利用して燃料状態とインベントリを直接構築する
            SetFuelState(powerGenerator, fuelItemId, remainingFuelTime);
            powerGenerator.SetItem(0, itemStackFactory.Create(fuelItemId, 5));
            powerGenerator.SetItem(2, itemStackFactory.Create(secondaryItemId, 5));

            // セーブ前の比較用スナップショットを取得する
            var expectedFuelState = CaptureFuelState(powerGenerator);
            var expectedInventory = CaptureInventorySnapshot(powerGenerator);

            
            
            // セーブデータを生成してディクショナリに格納する
            var states = powerGeneratorBlock.GetSaveState();

            
            
            // セーブデータを用いて発電機ブロックを復元する
            var blockGuid = generatorMaster.BlockGuid;
            var loadedPowerGeneratorBlock = blockFactory.Load(blockGuid, new BlockInstanceId(10), states, generatorPosInfo);
            var loadedPowerGenerator = loadedPowerGeneratorBlock.GetComponent<VanillaElectricGeneratorComponent>();

            // 復元後の燃料状態を検証する
            var loadedFuelState = CaptureFuelState(loadedPowerGenerator);
            Assert.AreEqual(expectedFuelState.CurrentFuelItemId, loadedFuelState.CurrentFuelItemId);
            Assert.AreEqual(expectedFuelState.RemainingFuelTime, loadedFuelState.RemainingFuelTime);
            Assert.AreEqual(expectedFuelState.FuelType, loadedFuelState.FuelType);

            // インベントリの内容が一致することを確認する
            var loadedInventory = CaptureInventorySnapshot(loadedPowerGenerator);
            Assert.AreEqual(expectedInventory.Count, loadedInventory.Count);
            for (var i = 0; i < fuelSlotCount; i++)
            {
                Assert.AreEqual(expectedInventory[i].ItemId, loadedInventory[i].ItemId);
                Assert.AreEqual(expectedInventory[i].Count, loadedInventory[i].Count);
            }

            #region Internal

            void SetFuelState(VanillaElectricGeneratorComponent component, ItemId itemId, double fuelTime)
            {
                // 燃料サービスのプライベートフィールドを書き換えて任意の燃焼状態を作る
                var fuelService = GetFuelService(component);
                var fuelServiceType = fuelService.GetType();
                var fuelTypeEnum = fuelServiceType.GetNestedType("FuelType", BindingFlags.NonPublic);
                var fuelTypeValue = Enum.Parse(fuelTypeEnum, "Item");

                fuelServiceType
                    .GetField("_currentFuelItemId", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(fuelService, itemId);
                fuelServiceType
                    .GetField("_currentFuelType", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(fuelService, fuelTypeValue);
                fuelServiceType
                    .GetField("_remainingFuelTime", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(fuelService, fuelTime);
                fuelServiceType
                    .GetField("_currentFuelFluidId", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(fuelService, FluidMaster.EmptyFluidId);

                var fuelContainerField = fuelServiceType.GetField("_fuelFluidContainer", BindingFlags.NonPublic | BindingFlags.Instance);
                var fuelContainer = (FluidContainer)fuelContainerField.GetValue(fuelService);
                if (fuelContainer != null)
                {
                    // 併設された液体タンクが存在する場合は流体情報も初期化しておく
                    fuelContainer.FluidId = fuelFluidId;
                    fuelContainer.Amount = 0;
                }
            }

            (ItemId CurrentFuelItemId, double RemainingFuelTime, string FuelType) CaptureFuelState(VanillaElectricGeneratorComponent component)
            {
                // 燃料サービスから現在の燃焼状況を読み取って比較用データを作成する
                var fuelService = GetFuelService(component);
                var fuelServiceType = fuelService.GetType();

                var currentFuelItemId = (ItemId)fuelServiceType
                    .GetField("_currentFuelItemId", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(fuelService);
                var remainingFuel = (double)fuelServiceType
                    .GetField("_remainingFuelTime", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(fuelService);
                var fuelType = fuelServiceType
                    .GetField("_currentFuelType", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(fuelService)
                    .ToString();

                return (currentFuelItemId, remainingFuel, fuelType);
            }

            List<(ItemId ItemId, int Count)> CaptureInventorySnapshot(VanillaElectricGeneratorComponent component)
            {
                // オープン可能インベントリの状態を一覧化する
                var inventoryService = (OpenableInventoryItemDataStoreService)typeof(VanillaElectricGeneratorComponent)
                    .GetField("_itemDataStoreService", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(component);

                var snapshot = new List<(ItemId, int)>(inventoryService.InventoryItems.Count);
                foreach (var stack in inventoryService.InventoryItems)
                {
                    snapshot.Add((stack.Id, stack.Count));
                }

                return snapshot;
            }

            object GetFuelService(VanillaElectricGeneratorComponent component)
            {
                // コンポーネントが保持する燃料サービスを取得する
                return typeof(VanillaElectricGeneratorComponent)
                    .GetField("_fuelService", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(component);
            }

            #endregion
        }
    }
}
