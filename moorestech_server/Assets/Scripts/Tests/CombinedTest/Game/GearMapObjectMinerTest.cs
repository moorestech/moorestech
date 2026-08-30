using System;
using Core.Update;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Map;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class GearMapObjectMinerTest
    {
        // テストマスタの装飾物。配置には無いので直接構築する
        // The decoration in the test master; it has no placement, so it is constructed directly
        private static readonly Guid DecorationMapObjectGuid = Guid.Parse("00000000-0000-4444-0000-000000000001");

        [Test]
        public void MapObjectMinerMiningTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var gearMapObjectMinerPosition = Vector3Int.zero;
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMapObjectMiner, gearMapObjectMinerPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var infinityTorqueSimpleGearGeneratorPosition = new Vector3Int(1, 0, 0);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, infinityTorqueSimpleGearGeneratorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 新formula: operatingRate = (rpm/baseRpm) * min(torque/reqTorque, 1)
            // New formula: at rpm=10, baseRpm=5 -> rpmRatio=2, reqTorque=1*(10/5)^2=4
            // InfinityGenerator provides 1M torque -> torqueRate=1, operatingRate=2
            // subTicks per frame = currentPower/requestPower = (basePower*operatingRate)/basePower = 2
            // miningTime=2s=40 ticks, done in 40/2=20 real ticks=1.0s exactly
            // Guard against exact boundary: check at 0.5s (safe before 1.0s completion)
            // 0.5s前に採掘未完了確認 -> 1.1s時点で採掘完了確認（1.0sで採掘完了するため）

            // 0.5秒間採掘（tick数で制御）。採掘時間は1.0秒（operatingRate=2で半分に短縮）
            // Mining for 0.5 second. Mining completes at 1.0s due to operatingRate=2.
            var halfSecondTicks = (int)(0.5 * GameUpdater.TicksPerSecond);
            for (var i = 0; i < halfSecondTicks; i++) GameUpdater.RunFrames(1);

            // まだインベントリに木がないことを確認する
            // Make sure there is no wood in the inventory yet
            var blockInChestComponent = block.GetComponent<VanillaChestComponent>();
            Assert.AreEqual(0, (int)blockInChestComponent.InventoryItems[0].Id);

            // 0.6秒間採掘（tick数で制御）。合計1.1秒時点でアイテムが入っているはず
            // Mining for 0.6 seconds. Total 1.1s: items should be in inventory.
            var pointSixSecondTicks = (int)(0.6 * GameUpdater.TicksPerSecond);
            for (var i = 0; i < pointSixSecondTicks; i++) GameUpdater.RunFrames(1);

            // インベントリにアイテムが入っていることを確認する（1.0sで採掘完了）
            // Make sure there is wood in the inventory (mining completes at 1.0s)
            Assert.AreEqual(2, (int)blockInChestComponent.InventoryItems[0].Id);

            // 範囲内にmap objectが2つあるのでアイテムが2個ある事をチェックする
            // There are two map objects in the range, so check that there are two items
            Assert.AreEqual(2, (int)blockInChestComponent.InventoryItems[0].Count);

            // 1.1秒間採掘（tick数で制御）。合計2.2秒時点で2サイクル目が完了（2.0sで完了）
            // Mining for 1.1 seconds. Total 2.2s: 2nd cycle completes at 2.0s.
            var onePointOneSecondTicks = (int)(1.1 * GameUpdater.TicksPerSecond);
            for (var i = 0; i < onePointOneSecondTicks; i++) GameUpdater.RunFrames(1);

            // インベントリのアイテムが増えていることを確認する
            // Make sure the inventory items have increased
            Assert.AreEqual(4, (int)blockInChestComponent.InventoryItems[0].Count);
        }

        [Test]
        public void 採掘設定に載った装飾物は採掘機の対象から外れHPも減らない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 採掘機の生成前に採掘範囲内へ装飾物を置く。対象収集は構築時に1回だけ走る
            // Place the decoration inside the mining area before the miner is built; targets are collected once at construction
            var decoration = new VanillaStaticMapObject(200, DecorationMapObjectGuid, false, 10, Vector3.zero);
            ServerContext.MapObjectDatastore.Add(decoration);

            var gearMapObjectMinerPosition = Vector3Int.zero;
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMapObjectMiner, gearMapObjectMinerPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var infinityTorqueSimpleGearGeneratorPosition = new Vector3Int(1, 0, 0);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, infinityTorqueSimpleGearGeneratorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 採掘サイクル(1.0s)を複数回跨ぐまで回す。弾けていなければ初回サイクルでattackHp10により破壊される
            // Run past several 1.0s mining cycles; if it were not excluded the first cycle would destroy it with attackHp 10
            var threeSecondTicks = 3 * GameUpdater.TicksPerSecond;
            for (var i = 0; i < threeSecondTicks; i++) GameUpdater.RunFrames(1);

            Assert.IsFalse(decoration.IsDestroyed);
            Assert.AreEqual(10, decoration.CurrentHp);
        }
    }
}
