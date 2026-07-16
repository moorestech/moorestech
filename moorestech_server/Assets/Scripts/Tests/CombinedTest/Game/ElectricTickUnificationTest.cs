using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.GearToElectric;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    /// <summary>
    /// 電力tick一元化の回帰テスト。孤立機械の自然停止とバッテリー残量のセーブ決定論を検証する
    /// Regression tests for the unified electric tick: isolated machines naturally stop and battery remainders survive saves deterministically
    /// </summary>
    public class ElectricTickUnificationTest
    {
        [Test]
        public void 両topology再構築は両需給計算より先に登録される()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var electricDatastore = provider.GetRequiredService<IElectricWireNetworkDatastore>();
            var gearDatastore = provider.GetRequiredService<GearNetworkDatastore>();
            var electricUpdater = provider.GetRequiredService<ElectricTickUpdater>();
            var gearUpdater = provider.GetRequiredService<GearTickUpdater>();

            // 登録delegateをtest側で照合する
            // Match delegate targets and methods from the test side without production diagnostics
            Assert.AreEqual(4, GameUpdater.AdditionalUpdates.Count);
            Assert.AreEqual((Action)electricDatastore.RebuildIfDirty, GameUpdater.AdditionalUpdates[0]);
            Assert.AreEqual((Action)gearDatastore.RebuildIfDirty, GameUpdater.AdditionalUpdates[1]);
            Assert.AreEqual((Action)electricUpdater.Update, GameUpdater.AdditionalUpdates[2]);
            Assert.AreEqual((Action)gearUpdater.Update, GameUpdater.AdditionalUpdates[3]);
        }

        // 発電機を撤去された機械は、供給率0の導出により自然に停止する
        // A machine whose generator is removed derives supply rate 0 and naturally stops
        [Test]
        public void IsolatedElectricMachineStops()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
            world.TryAddBlock(ForUnitTestModBlockId.InfinityGeneratorId, new Vector3Int(0, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 2));

            // 接続反映後は供給率1の実効電力が届く
            // After the topology flush, effective power arrives at rate 1
            var processor = machine.GetComponent<VanillaMachineProcessorComponent>();
            GameUpdater.RunFrames(2);
            Assert.Greater(processor.CurrentPower, 0f);

            // 発電機を撤去すると全電線切断となり、次のtickから供給率0で停止する
            // Removing the generator cuts every wire, so the machine derives rate 0 and stops from the next tick
            world.RemoveBlock(new Vector3Int(0, 0, 2), BlockRemoveReason.ManualRemove);
            GameUpdater.RunFrames(2);
            Assert.AreEqual(0f, processor.CurrentPower);
        }

        // 歯車→電力変換機のバッテリー残量はセーブ・ロードを跨いで維持される
        // The gear-to-electric converter's battery remainder survives save and load
        [Test]
        public void GearToElectricBatterySurvivesSaveLoad()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.TestGearToElectricGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out var driveBlock);

            var component = generatorBlock.GetComponent<GearToElectricGeneratorComponent>();
            var drive = driveBlock.GetComponent<SimpleGearGeneratorComponent>();
            var param = (GearToElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator).BlockParam;

            // フル回転で満充電し、利用率40%で放電して残量60%を作る
            // Fully charge at full rotation, then discharge at 40% utilization to leave a 60% remainder
            drive.SetGenerateRpm((float)param.GearConsumption.BaseRpm);
            drive.SetGenerateTorque((float)param.GearConsumption.BaseTorque);
            GameUpdater.RunFrames(20);
            drive.SetGenerateRpm(0f);
            GameUpdater.RunFrames(2);
            var full = component.OutputEnergy().AsPrimitive();
            Assert.AreEqual(param.MaxGeneratedPower, full, 0.1f);
            component.OnElectricTickPostProcess(new ElectricNetworkStatistics(full, full * 0.4f, 1f, 1));
            var savedRemaining = component.OutputEnergy().AsPrimitive();
            Assert.AreEqual(full * 0.6f, savedRemaining, 0.01f);

            // GetSaveState→BlockFactory.Loadの往復で残量が一致する
            // The remainder matches across a GetSaveState → BlockFactory.Load roundtrip
            var saveState = generatorBlock.GetSaveState();
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator);
            var positionInfo = new BlockPositionInfo(new Vector3Int(10, 0, 10), BlockDirection.North, Vector3Int.one);
            var loadedBlock = ServerContext.BlockFactory.Load(blockMaster.BlockGuid, new BlockInstanceId(999), saveState, positionInfo);
            var loadedComponent = loadedBlock.GetComponent<GearToElectricGeneratorComponent>();

            Assert.AreEqual(savedRemaining, loadedComponent.OutputEnergy().AsPrimitive(), 0.001f);
        }

        // 電力→歯車変換機のバッテリー残量と選択モードはセーブ・ロードを跨いで維持される
        // The electric-to-gear converter's battery remainder and selected mode survive save and load
        [Test]
        public void ElectricToGearBatterySurvivesSaveLoad()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var component = block.GetComponent<ElectricToGearGeneratorComponent>();
            var param = (ElectricToGearGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestElectricToGearGenerator).BlockParam;

            // 供給率0.5の電力tickで半分だけ充電された状態を作る
            // Charge exactly half of the battery with a rate-0.5 electric tick
            var mode0 = param.OutputModes[0];
            component.OnElectricTickPostProcess(new ElectricNetworkStatistics((float)mode0.RequiredPower * 0.5f, (float)mode0.RequiredPower, 0.5f, 1));
            var savedRemaining = ReadBatteryRemaining(component);
            Assert.AreEqual((float)mode0.RequiredPower * 0.5f, savedRemaining, 0.001f);

            var saveState = block.GetSaveState();
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestElectricToGearGenerator);
            var positionInfo = new BlockPositionInfo(new Vector3Int(10, 0, 10), BlockDirection.North, Vector3Int.one);
            var loadedBlock = ServerContext.BlockFactory.Load(blockMaster.BlockGuid, new BlockInstanceId(998), saveState, positionInfo);
            var loadedComponent = loadedBlock.GetComponent<ElectricToGearGeneratorComponent>();

            // 残量が維持されるため、供給率0.5の1tickで満充電になり脈動位相も保たれる
            // The remainder is kept, so one more rate-0.5 tick reaches full charge, preserving the pulse phase
            Assert.AreEqual(savedRemaining, ReadBatteryRemaining(loadedComponent), 0.001f);
            loadedComponent.OnElectricTickPostProcess(new ElectricNetworkStatistics((float)mode0.RequiredPower * 0.5f, (float)mode0.RequiredPower, 0.5f, 1));
            Assert.AreEqual((float)mode0.Torque, loadedComponent.GenerateTorque.AsPrimitive(), 0.01f);

            #region Internal

            // 表示用ステート詳細からバッテリー残量を読み取る
            // Read the battery remainder from the display state detail
            float ReadBatteryRemaining(ElectricToGearGeneratorComponent target)
            {
                var detail = target.GetBlockStateDetails()[0];
                var deserialized = MessagePackSerializer.Deserialize<ElectricToGearGeneratorBlockStateDetail>(detail.Value);
                return deserialized.BatteryRemaining;
            }

            #endregion
        }
    }
}
