using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using Game.Orbit;
using Game.PlayerIdentity;
using Game.Research;
using Game.SpaceElevator;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Game.SpaceElevator;
using Tests.Module.TestMod;
using Tests.Util;
using Tests.Util.PlayerIdentity;
using Tests.Util.Rocket;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;
using static Tests.CombinedTest.Game.Tier9.Tier9ComputeLoopTestHelper;
using Ids = Tests.Module.TestMod.ForUnitTestTier9ChainIds;

namespace Tests.CombinedTest.Game.Tier9
{
    /// <summary>
    ///     ICチップから拠点の昇降までを1本で通す
    ///     Runs the chain from IC chip to elevator climb in one test
    /// </summary>
    public class Tier9ComputeLoopEndToEndTest
    {
        [Test]
        public void ICチップから計算データの研究と拠点の昇降まで通る()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(provider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var research = provider.GetRequiredService<IResearchDataStore>();
            var unlocks = provider.GetRequiredService<IGameUnlockStateDataController>();
            var craftCaps = provider.GetRequiredService<ILevelInheritanceCraftCapLookup>();

            // 発射台は最後に置く（網の再構築でテスト発電機が外れるため）
            // Place the powered pad last (rebuilds drop injected generators)
            var basePosition = new Vector3Int(10, 0, 0);
            var elevatorBase = SpaceElevatorTestUtil.PlaceBase(basePosition);
            var (gpuBlock, gpuInventory, gpuProcessor) = PlaceMachine(new Vector3Int(20, 0, 0));
            var (sat1Block, sat1Inventory, sat1Processor) = PlaceMachine(new Vector3Int(24, 0, 0));
            var (sat2Block, sat2Inventory, sat2Processor) = PlaceMachine(new Vector3Int(28, 0, 0));
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            MachineRecipeSelectTestUtil.SelectRecipe(gpuBlock, Recipe(Ids.GpuRecipeGuid));
            MachineRecipeSelectTestUtil.SelectRecipe(sat1Block, Recipe(Ids.GpuSatelliteLv1RecipeGuid));
            MachineRecipeSelectTestUtil.SelectRecipe(sat2Block, Recipe(Ids.GpuSatelliteLv2RecipeGuid));

            // 上限1でLv2を断り、Lv1でGPULv1を2個作る
            // Cap 1 refuses Lv2; Lv1 chips make two GPU Lv1
            Assert.AreEqual(1, craftCaps.GetCraftCap(Ids.GpuBaseGuid));
            Assert.AreEqual(MachineSlotPlacementCheck.LevelAboveCraftCap, gpuInventory.CheckPlacement(0, Create(Ids.IcChip(2), 1)));
            Assert.AreEqual(2, gpuInventory.InsertItem(Create(Ids.IcChip(2), 2)).Count);
            Assert.AreEqual(0, gpuInventory.InsertItem(Create(Ids.IcChip(1), 4)).Count);
            RunOneCraft(gpuProcessor, Ids.GpuRecipeGuid);
            RunOneCraft(gpuProcessor, Ids.GpuRecipeGuid);
            Assert.AreEqual(2, CountItem(gpuInventory, InputSlotCount, InputSlotCount + OutputSlotCount, Ids.Gpu(1)));

            // 衛星Lv1 を2個作る（打ち上げ用と拠点用）
            // Make two satellite Lv1 (to launch and to climb)
            Assert.AreEqual(0, sat1Inventory.InsertItem(TakeOutput(gpuInventory, Ids.Gpu(1), 2)).Count);
            Assert.AreEqual(0, sat1Inventory.InsertItem(CreateTest1(2)).Count);
            RunOneCraft(sat1Processor, Ids.GpuSatelliteLv1RecipeGuid);
            RunOneCraft(sat1Processor, Ids.GpuSatelliteLv1RecipeGuid);
            Assert.AreEqual(2, CountItem(sat1Inventory, InputSlotCount, InputSlotCount + OutputSlotCount, Ids.GpuSatellite(1)));

            // ロケット技術で発射台を解放し初打ち上げ
            // Rocket technology unlocks the pad, then the first launch
            Assert.AreEqual(ResearchNodeState.UnresearchableAllReasons, research.GetResearchNodeStates(playerId)[Ids.GpuUpgradeLv2ResearchGuid]);
            Assert.IsFalse(unlocks.BlockUnlockStateInfos[Ids.RocketLaunchPadBlockGuid].IsUnlocked);
            Assert.IsTrue(research.CompleteResearch(Ids.RocketTechnologyResearchGuid, playerId));
            Assert.IsTrue(unlocks.BlockUnlockStateInfos[Ids.RocketLaunchPadBlockGuid].IsUnlocked);
            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughItem, research.GetResearchNodeStates(playerId)[Ids.GpuUpgradeLv2ResearchGuid]);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, TakeOutput(sat1Inventory, Ids.GpuSatellite(1), 1).Id, 1);
            LaunchFirst(pad);
            Assert.IsTrue(ServerContext.GetService<RocketLaunchHistory>().HasLaunchedOnce);
            Assert.AreEqual(10, MovePadOutputsToPlayer(pad, playerId));

            // 計算データで上限2にし GPULv2 を作る
            // Compute data raises the cap to 2 and GPU Lv2 is made
            Assert.AreEqual(ResearchNodeState.Researchable, research.GetResearchNodeStates(playerId)[Ids.GpuUpgradeLv2ResearchGuid]);
            Assert.IsTrue(research.CompleteResearch(Ids.GpuUpgradeLv2ResearchGuid, playerId));
            Assert.AreEqual(0, CountPlayerItem(playerId, ForUnitTestItemId.ComputeDataId));
            Assert.AreEqual(2, craftCaps.GetCraftCap(Ids.GpuBaseGuid));
            Assert.AreEqual(MachineSlotPlacementCheck.Allowed, gpuInventory.CheckPlacement(0, Create(Ids.IcChip(2), 1)));
            Assert.AreEqual(0, gpuInventory.InsertItem(Create(Ids.IcChip(2), 2)).Count);
            RunOneCraft(gpuProcessor, Ids.GpuRecipeGuid);
            Assert.AreEqual(1, CountItem(gpuInventory, InputSlotCount, InputSlotCount + OutputSlotCount, Ids.Gpu(2)));

            // 衛星Lv2 の2便目は合図なしで飛ぶ
            // The second flight with satellite Lv2 needs no command
            Assert.AreEqual(0, sat2Inventory.InsertItem(TakeOutput(gpuInventory, Ids.Gpu(2), 1)).Count);
            Assert.AreEqual(0, sat2Inventory.InsertItem(CreateTest1(1)).Count);
            RunOneCraft(sat2Processor, Ids.GpuSatelliteLv2RecipeGuid);
            LaunchNext(pad, TakeOutput(sat2Inventory, Ids.GpuSatellite(2), 1).Id, 1);
            Assert.AreEqual(20, MovePadOutputsToPlayer(pad, playerId));

            // 軌道エレベーターの建設で拠点を解放
            // Orbital elevator construction unlocks the base
            Assert.IsFalse(unlocks.BlockUnlockStateInfos[Ids.SpaceElevatorBaseBlockGuid].IsUnlocked);
            Assert.IsTrue(research.CompleteResearch(Ids.OrbitalElevatorConstructionResearchGuid, playerId));
            Assert.IsTrue(unlocks.BlockUnlockStateInfos[Ids.SpaceElevatorBaseBlockGuid].IsUnlocked);
            Assert.AreEqual(10, CountPlayerItem(playerId, ForUnitTestItemId.ComputeDataId));

            // ケーブル2便でプール100にし接続
            // Two cable flights fill the pool to 100, then connect
            var pool = ServerContext.GetService<SpaceElevatorCablePool>();
            LaunchNext(pad, ForUnitTestItemId.TestElevatorCable, 50);
            Assert.AreEqual(50, pool.Amount);
            LaunchNext(pad, ForUnitTestItemId.TestElevatorCable, 50);
            Assert.AreEqual(100, pool.Amount);
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, ServerContext.GetService<SpaceElevatorConnectionService>().Connect(elevatorBase));
            Assert.AreEqual(0, pool.Amount);

            // 給電した拠点で衛星Lv1 が昇る
            // Satellite Lv1 climbs on the powered base
            SpaceElevatorTestUtil.PowerBase(elevatorBase, basePosition, 100f);
            var baseInventory = elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>();
            Assert.AreEqual(0, baseInventory.InsertItem(TakeOutput(sat1Inventory, Ids.GpuSatellite(1), 1)).Count);
            SpaceElevatorTestUtil.RunSeconds(2.2);
            Assert.AreEqual(0, baseInventory.GetItem(0).Count);
            Assert.AreEqual(ForUnitTestItemId.ComputeDataId, baseInventory.GetItem(1).Id);
            Assert.AreEqual(10, baseInventory.GetItem(1).Count);
        }
    }
}
