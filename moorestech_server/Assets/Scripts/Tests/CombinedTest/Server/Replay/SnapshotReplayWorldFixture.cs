using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
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
    // ベルト搬送とレシピ加工が毎tick動く世界を組む。静的な世界では決定性検査が素通りしてしまうため
    // Builds a world where belt transport and recipe processing move every tick; a static world would let the determinism check pass vacuously
    public static class SnapshotReplayWorldFixture
    {
        private static readonly string BeltSaveKey = typeof(VanillaBeltConveyorComponent).FullName;
        private static readonly string MachineSaveKey = typeof(VanillaMachineSaveComponent).FullName;

        private static readonly Vector3Int BeltHeadPosition = new(0, 0, 0);
        private static readonly Vector3Int PolePosition = new(5, 0, 0);
        private static readonly Vector3Int MachinePosition = new(7, 0, 0);
        private static readonly Vector3Int GeneratorPosition = new(5, 0, 2);

        public static void BuildMovingWorld()
        {
            var world = ServerContext.WorldBlockDatastore;

            // 3連ベルト（1本40tick）の先頭へアイテムを入れる。計測中ずっと搬送中で、スナップショット境界ごとに位置が変わる
            // Insert an item at the head of three chained belts (40 ticks each); it stays in transit and shifts at every snapshot boundary
            for (var z = 0; z < 3; z++)
            {
                world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            }
            var belt = world.GetBlock(BeltHeadPosition).GetComponent<VanillaBeltConveyorComponent>();
            var remainder = belt.InsertItem(ServerContext.ItemStackFactory.Create(new ItemId(1), 1), InsertItemContext.Empty);
            Assert.AreEqual(ItemMaster.EmptyItemId, remainder.Id, "ベルトへのアイテム投入に失敗した");

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
        }

        // 搬送中アイテムと加工中レシピがスナップショットに実際に入っていることを確かめる
        // Verify the snapshot actually carries an item in transit and a recipe mid-processing
        public static void AssertDynamicStatePresent(string snapshotJson)
        {
            Assert.IsNotEmpty(BeltItems(snapshotJson), "ベルト上に搬送中アイテムが無い。フィクスチャが静的化している");

            var processor = MachineProcessor(snapshotJson);
            Assert.AreEqual((int)ProcessState.Processing, processor["state"].Value<int>(), "機械が加工中でない。フィクスチャが静的化している");
            Assert.Greater(processor["remainingSeconds"].Value<double>(), 0d, "加工の残り時間が0。フィクスチャが静的化している");
        }

        // 2つのスナップショットの間で動的状態が実際に変化したことを確かめる。ブロック数が同じ区間で呼ぶこと（配列長差で比較が打ち切られるため）
        // Verify the dynamic state actually changed between two snapshots; call it on an interval with the same block count, since a length mismatch cuts the walk short
        public static void AssertDynamicStateChanged(string earlierSnapshotJson, string laterSnapshotJson)
        {
            var moved = SnapshotJsonComparer.Compare(earlierSnapshotJson, laterSnapshotJson).Differences;
            Assert.IsTrue(moved.Any(difference => difference.Contains(BeltSaveKey)), "スナップショット間でベルト搬送が進んでいない:\n" + string.Join("\n", moved));
            Assert.IsTrue(moved.Any(difference => difference.Contains("remainingSeconds")), "スナップショット間でレシピ加工が進んでいない:\n" + string.Join("\n", moved));
        }

        private static List<JToken> BeltItems(string snapshotJson)
        {
            var states = JObject.Parse(snapshotJson).SelectTokens($"$.world[*].state['{BeltSaveKey}']");
            return states.SelectMany(state => state.Children()).Where(item => item.Type != JTokenType.Null).ToList();
        }

        private static JToken MachineProcessor(string snapshotJson)
        {
            var processors = JObject.Parse(snapshotJson).SelectTokens($"$.world[*].state['{MachineSaveKey}'].processor").ToList();
            Assert.AreEqual(1, processors.Count, "機械ブロックの加工状態がスナップショットに1件だけ存在するはず");
            return processors[0];
        }
    }
}
