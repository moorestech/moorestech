using Tests.Util.EnergySystem;
using System;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.SaveLoad.Snapshot;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.Replay
{
    // 燃料燃焼・レシピ加工・機械から搬入される合流ベルトが毎tick動く世界を組む。静的な世界では決定性検査が素通りしてしまうため
    // Builds a world where fuel burning, recipe processing and a machine-fed merging belt move every tick; a static world would let the determinism check pass vacuously
    public static class SnapshotReplayWorldFixture
    {
        private static readonly string GeneratorSaveKey = typeof(VanillaElectricGeneratorComponent).FullName;
        private static readonly string MachineSaveKey = typeof(VanillaMachineSaveComponent).FullName;

        // 燃焼時間2秒(=40tick)の燃料。スナップショット間隔20tickと周期がずれるので、境界ごとに残り燃焼時間が変わる
        // A fuel burning 2 s (= 40 ticks); its period differs from the 20-tick snapshot interval, so the remaining burn time differs at every boundary
        private static readonly Guid FuelItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        private static readonly Vector3Int FuelGeneratorPosition = new(0, 0, 0);
        private static readonly Vector3Int PolePosition = new(5, 0, 0);
        private static readonly Vector3Int MachinePosition = new(7, 0, 0);
        private static readonly Vector3Int GeneratorPosition = new(5, 0, 2);
        // 合流ベルトの列。他の設備から離して置く
        // Column of the merging belt line, kept clear of the other installations
        private const int BeltColumnX = 20;

        public static void BuildMovingWorld()
        {
            var world = ServerContext.WorldBlockDatastore;

            // 未配線の燃料発電機へ燃料10個(計400tick分)を入れる。計測中ずっと燃焼し、セーブされる残り燃焼時間が毎tick減る
            // Load ten fuels (400 ticks in total) into an unwired fuel generator; it burns throughout and its saved remaining burn time drops every tick
            world.TryAddBlock(ForUnitTestModBlockId.GeneratorId, FuelGeneratorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fuelGenerator);
            var fuel = ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(FuelItemGuid), 10);
            var remainder = fuelGenerator.GetComponent<VanillaElectricGeneratorComponent>().InsertItem(fuel);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainder.Id, "発電機への燃料投入に失敗した");

            // 電柱・機械・無限発電機を配線してレシピ3周分の材料を入れる。加工は計測中止まらない
            // Wire a pole, machine, and infinite generator, then load three recipe runs' worth of inputs so processing never stalls
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var machineId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            world.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, PolePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(machineId, MachinePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
            world.TryAddBlock(ForUnitTestModBlockId.InfinityGeneratorId, GeneratorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(PolePosition, MachinePosition);
            ElectricWireTestUtil.Connect(PolePosition, GeneratorPosition);

            MachineRecipeSelectTestUtil.SelectRecipe(machine, recipe);
            var machineInventory = machine.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var input in recipe.InputItems)
            {
                machineInventory.InsertItem(ServerContext.ItemStackFactory.Create(input.ItemGuid, input.Count * 3));
            }

            BuildMachineFedMergingBelt();
        }

        // チェスト→直線(z=-1..3)→チェスト。z=1へ横のベルコン、z=2へ横の機械が合流し、ロード復元の距離・優先順・個体IDを検査対象にする
        // Chest -> line (z=-1..3) -> chest; a side belt merges at z=1 and a side machine at z=2, so load-restored distances, priority orders and instance ids are checked
        private static void BuildMachineFedMergingBelt()
        {
            var item = MasterHolder.ItemMaster.GetItemId(FuelItemGuid);
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(item);
            var sourceMain = PlaceChest(new Vector3Int(BeltColumnX, 0, -2));
            for (var z = -1; z <= 3; z++) PlaceBelt(new Vector3Int(BeltColumnX, 0, z), BlockDirection.North);
            PlaceBelt(new Vector3Int(BeltColumnX + 1, 0, 1), BlockDirection.West);
            var sourceSide = PlaceChest(new Vector3Int(BeltColumnX + 2, 0, 1));
            var sourceMachineMerge = PlaceChest(new Vector3Int(BeltColumnX - 1, 0, 2));
            PlaceChest(new Vector3Int(BeltColumnX, 0, 4));

            // 3つの搬入元を満杯にし、計測中ずっとベルトへ押し込ませる
            // Fill the three sources so they keep pushing into the belts throughout the run
            foreach (var source in new[] { sourceMain, sourceSide, sourceMachineMerge }) source.SetItem(0, ServerContext.ItemStackFactory.Create(item, maxStack));

            #region Internal

            IOpenableBlockInventoryComponent PlaceChest(Vector3Int position)
            {
                ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest);
                return chest.ComponentManager.GetComponent<IOpenableBlockInventoryComponent>();
            }

            void PlaceBelt(Vector3Int position, BlockDirection direction)
            {
                ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, position, direction, Array.Empty<BlockCreateParam>(), out _);
            }

            #endregion
        }

        // 燃焼中の燃料と加工中レシピがスナップショットに実際に入っていることを確かめる
        // Verify the snapshot actually carries a burning fuel and a recipe mid-processing
        public static void AssertDynamicStatePresent(string snapshotJson)
        {
            Assert.Greater(RemainingFuelSeconds(snapshotJson), 0d, "発電機が燃焼中でない。フィクスチャが静的化している");

            var processor = MachineProcessor(snapshotJson);
            Assert.AreEqual((int)ProcessState.Processing, processor["state"].Value<int>(), "機械が加工中でない。フィクスチャが静的化している");
            Assert.Greater(processor["remainingSeconds"].Value<double>(), 0d, "加工の残り時間が0。フィクスチャが静的化している");
        }

        // 2つのスナップショットの間で動的状態が実際に変化したことを確かめる。燃焼・加工ともに値そのものを比較する
        // Verify the dynamic state actually changed between two snapshots, comparing the burn and processing values directly
        public static void AssertDynamicStateChanged(string earlierSnapshotJson, string laterSnapshotJson)
        {
            Assert.AreNotEqual(RemainingFuelSeconds(earlierSnapshotJson), RemainingFuelSeconds(laterSnapshotJson), "スナップショット間で燃料の燃焼が進んでいない");

            var earlierRemainingSeconds = MachineProcessor(earlierSnapshotJson)["remainingSeconds"].Value<double>();
            var laterRemainingSeconds = MachineProcessor(laterSnapshotJson)["remainingSeconds"].Value<double>();
            Assert.AreNotEqual(earlierRemainingSeconds, laterRemainingSeconds, "スナップショット間でレシピ加工が進んでいない");
        }

        private static double RemainingFuelSeconds(string snapshotJson)
        {
            // 無限発電機も同じ保存キーを持つので、燃料発電機の座標で絞る
            // The infinity generator shares the save key, so narrow down by the fuel generator's position
            var states = JObject.Parse(snapshotJson).SelectTokens("$.world[*]")
                .Where(block => block["X"].Value<int>() == FuelGeneratorPosition.x && block["Y"].Value<int>() == FuelGeneratorPosition.y && block["Z"].Value<int>() == FuelGeneratorPosition.z)
                .Select(block => block["state"][GeneratorSaveKey]).ToList();
            Assert.AreEqual(1, states.Count, "燃料発電機の状態がスナップショットに1件だけ存在するはず");
            return states[0]["remainingFuelSeconds"].Value<double>();
        }

        private static JToken MachineProcessor(string snapshotJson)
        {
            var processors = JObject.Parse(snapshotJson).SelectTokens($"$.world[*].state['{MachineSaveKey}'].processor").ToList();
            Assert.AreEqual(1, processors.Count, "機械ブロックの加工状態がスナップショットに1件だけ存在するはず");
            return processors[0];
        }
    }
}
