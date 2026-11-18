using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Construction;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Source;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using Game.Construction;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BuildMenu.Source
{
    public class BlueprintBuildMenuMaterialTest
    {
        private DebugParametersIsolationScope _debugScope;
        private ClientRemainingPlacementCountDatastore _remaining;

        [SetUp]
        public void SetUp()
        {
            // 開発者の設定を使わずテストごとに無料フラグを隔離する
            // Isolate the free-placement flag per test from developer settings
            _debugScope = DebugParametersIsolationScope.Begin(nameof(BlueprintBuildMenuMaterialTest));
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _remaining = new ClientRemainingPlacementCountDatastore();
        }

        [TearDown]
        public void TearDown() => _debugScope.End();

        [Test]
        public void BPエントリの必要素材はBP全ブロック分で所持0なら不足Test()
        {
            // ChestIdは素材無料なので有料ブロック2個で不足を検査する
            // ChestId has no cost, so test shortages with two paid blocks
            var block = ForUnitTestModBlockId.BlockId;
            var costs = MasterHolder.BlockMaster.GetBlockMaster(block).RequiredItems;
            Assert.IsNotEmpty(costs);
            var entry = CreateEntry(CreateBlueprint(block, block));
            Assert.AreEqual(costs.Length, entry.Materials.Count);
            foreach (var cost in costs)
            {
                var row = entry.Materials.Single(line => line.Row.ItemId == MasterHolder.ItemMaster.GetItemId(cost.ItemGuid));
                Assert.AreEqual(cost.Count * 2, row.Row.Item.Count);
                Assert.AreEqual(0, row.Row.Item.Held);
                Assert.IsTrue(row.IsInsufficient);
            }
            Assert.IsFalse(entry.Catalog.PaymentWaived);
        }

        [Test]
        public void 財布の残りで賄えるセルはBPエントリでも要求しないTest()
        {
            var block = ForUnitTestModBlockId.GearBeltConveyor;
            _remaining.Apply(block, 3);
            var entry = CreateEntry(CreateBlueprint(block, block, block));
            Assert.IsEmpty(entry.Materials);
            Assert.AreSame(BuildMenuSetPlacementNone.Instance, entry.SetPlacement);
        }

        [Test]
        public void 財布の残りを超えた分だけ補充一セットを要求するTest()
        {
            var block = ForUnitTestModBlockId.GearBeltConveyor;
            _remaining.Apply(block, 2);
            var entry = CreateEntry(CreateBlueprint(block, block, block));
            var costs = MasterHolder.BlockMaster.GetBlockMaster(block).RequiredItems;
            Assert.IsNotEmpty(costs);
            foreach (var cost in costs)
                Assert.AreEqual(cost.Count, entry.Materials.Single(line => line.Row.ItemId == MasterHolder.ItemMaster.GetItemId(cost.ItemGuid)).Row.Item.Count);
        }

        [Test]
        public void 坂と直線の財布の残りを二重使用しないTest()
        {
            var block = ForUnitTestModBlockId.GearBeltConveyor;
            _remaining.Apply(block, 1);
            var entry = CreateEntry(CreateBlueprint(block, ForUnitTestModBlockId.TestGearBeltConveyorUp));
            var costs = MasterHolder.BlockMaster.GetBlockMaster(block).RequiredItems;
            Assert.IsNotEmpty(costs);
            Assert.AreEqual(costs.Length, entry.Materials.Count);
            foreach (var cost in costs)
            {
                var row = entry.Materials.Single(line => line.Row.ItemId == MasterHolder.ItemMaster.GetItemId(cost.ItemGuid));
                Assert.AreEqual(cost.Count, row.Row.Item.Count, "共有財布を一度だけ使い補充1セットを要求する");
                Assert.AreEqual(0, row.Row.Item.Held);
                Assert.IsTrue(row.IsInsufficient);
            }
        }

        [Test]
        public void 無料設置中のBPエントリは支払い免除Test()
        {
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
            var entry = CreateEntry(CreateBlueprint(ForUnitTestModBlockId.BlockId));
            Assert.IsTrue(entry.Catalog.PaymentWaived);
            Assert.IsNotEmpty(entry.Materials);
            // 免除でも素材の行は所持と必要の事実のまま（所持 0 なので全部足りない）
            // Even when waived the rows keep the held/required facts (nothing held, so every row falls short)
            Assert.IsTrue(entry.Materials.All(line => line.Row.Item.Held < line.Row.Item.Count));
        }

        [Test]
        public void 無料設置でもBPのチェーン代は不足として表示するTest()
        {
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
            var blueprint = CreateBlueprint(ForUnitTestModBlockId.GearChainPole, ForUnitTestModBlockId.GearChainPole);
            var chainGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");
            blueprint.Chains.Add(new BlueprintLineJsonObject(0, 1, chainGuid));
            Assert.IsTrue(ConnectToolCostCalculator.TryCalculate(chainGuid, 10, out var costs));
            Assert.IsNotEmpty(costs);
            var entry = CreateEntry(blueprint);
            Assert.IsFalse(entry.Catalog.PaymentWaived);
            Assert.AreEqual(costs.Count, entry.Materials.Count);
            foreach (var cost in costs)
            {
                var row = entry.Materials.Single(line => line.Row.ItemId == cost.ItemId);
                Assert.AreEqual(cost.Count, row.Row.Item.Count);
                Assert.IsTrue(row.IsInsufficient);
            }
        }

        [Test]
        public void 無料設置中の接続不能な重複チェーンは二重請求しないTest()
        {
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
            var blueprint = CreateBlueprint(ForUnitTestModBlockId.GearChainPole, ForUnitTestModBlockId.GearChainPole);
            var chainGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");
            blueprint.Chains.Add(new BlueprintLineJsonObject(0, 1, chainGuid));
            blueprint.Chains.Add(new BlueprintLineJsonObject(1, 0, chainGuid));
            var entry = CreateEntry(blueprint);
            // 受理される一本だけを表示し、完全免除にもならない
            // Show only the accepted chain and keep its payment requirement
            Assert.IsFalse(entry.Catalog.PaymentWaived);
            Assert.IsTrue(ConnectToolCostCalculator.TryCalculate(chainGuid, 10, out var costs));
            foreach (var cost in costs)
            {
                Assert.AreEqual(cost.Count, entry.Materials.Single(line => line.Row.ItemId == cost.ItemId).Row.Item.Count);
            }
        }

        [Test]
        public void 通常設置のBPには保存電線の素材も表示するTest()
        {
            var blueprint = CreateBlueprint(ForUnitTestModBlockId.ElectricPoleId, ForUnitTestModBlockId.ElectricPoleId);
            // テスト電柱の接続範囲内に両端を置く
            // Keep both test poles within their connection range.
            blueprint.Blocks[1].OffsetX = 2;
            var wireGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, wireGuid));
            Assert.IsTrue(BlueprintPasteCopyBuilder.BuildUnobstructed(blueprint).Lines.Single().IsConnectable);
            Assert.IsTrue(ConnectToolCostCalculator.TryCalculate(wireGuid, 2, out var costs));
            Assert.IsNotEmpty(costs);
            var entry = CreateEntry(blueprint);
            foreach (var cost in costs)
            {
                var blockCost = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricPoleId).RequiredItems
                    .Where(item => MasterHolder.ItemMaster.GetItemId(item.ItemGuid) == cost.ItemId).Sum(item => item.Count) * 2;
                Assert.AreEqual(cost.Count + blockCost,
                    entry.Materials.Single(line => line.Row.ItemId == cost.ItemId).Row.Item.Count);
            }
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
            Assert.IsTrue(CreateEntry(blueprint).Catalog.PaymentWaived);
        }

        private BuildMenuEntryViewModel CreateEntry(BlueprintJsonObject blueprint)
        {
            var target = new BlueprintPlacementTarget(blueprint.BlueprintGuid, blueprint.Name, blueprint);
            return BuildMenuEntryFactory.CreateEntries(new IPlacementTarget[] { target },
                new ConstructionWalletQuery(_remaining), Array.Empty<IItemStack>(), new BlueprintThumbnailContainer())[0];
        }

        private static BlueprintJsonObject CreateBlueprint(params BlockId[] blockIds)
        {
            // 最小角基準で重ならない列を配置
            // Place a non-overlapping run from extent-min origins.
            var blocks = new List<BlueprintBlockJsonObject>();
            for (var i = 0; i < blockIds.Length; i++)
                blocks.Add(new BlueprintBlockJsonObject(new Vector3Int(i * 10, 0, 0),
                    MasterHolder.BlockMaster.GetBlockMaster(blockIds[i]).BlockGuid.ToString(), (int)BlockDirection.North, new()));
            return new BlueprintJsonObject("materials", blocks, new(), new(), Guid.NewGuid());
        }
    }
}
