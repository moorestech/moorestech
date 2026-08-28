using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Miner;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Map.Interface.Vein;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.Util.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Core
{
    public class MinerMiningTest
    {
        //一定時間たったら鉱石が出るテスト
        [Test]
        public void MiningTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // 手動で鉱石の設定を行う
            var (_, pos) = GetItemMapVein();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricMinerId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var miner = worldBlockDatastore.GetBlock(pos);
            var minerComponent = miner.GetComponent<VanillaMinerProcessorComponent>();

            // 採掘機を電柱へ接続して電力網を成立させる
            // Connect the miner to a pole so it belongs to a usable electric network
            var polePosition = pos + new Vector3Int(2, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, polePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(pos, polePosition);
            
            var miningItems = (List<IItemStack>)typeof(VanillaMinerProcessorComponent).GetField("_miningItems", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(minerComponent);
            var miningItemId = miningItems[0].Id;
            var miningTicks = (uint)typeof(VanillaMinerProcessorComponent).GetField("_defaultMiningTicks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(minerComponent);
            
            
            // チェストを隣に設置し、アイテムがアウトプットされていることを確認する
            var chestBlockPos = new Vector3Int(pos.x + 1, pos.y, pos.z);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, chestBlockPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chestBlock);
            var chestComponent = chestBlock.GetComponent<VanillaChestComponent>();
            
            //電力の設定。採掘機が属するワイヤーセグメントへテスト発電機を登録する
            //Power setup: register a test generator into the wire segment the miner belongs to
            GameUpdater.UpdateOneTick();
            var networkDatastore = ServerContext.GetService<IElectricWireNetworkLookup>();
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(miner.BlockInstanceId, out var segment));
            AddGenerator(segment, new TestElectricGenerator(new ElectricPower(10000), new BlockInstanceId(10)));
            GameUpdater.UpdateOneTick();
            
            // tick数で採掘時間を計算（+2 tickのマージン）
            // Calculate mining time in ticks (with +2 ticks margin)
            var waitTicks = (int)(miningTicks + 2);

            //テストコードの準備完了
            //鉱石1個分の採掘時間待機（tick数で制御）
            // Wait for one ore mining time (controlled by tick count)
            for (var i = 0; i < waitTicks; i++) GameUpdater.RunFrames(1);
            
            //鉱石1個が出力されているかチェック
            Assert.AreEqual(miningItemId, chestComponent.InventoryItems[0].Id);
            Assert.AreEqual(1, chestComponent.InventoryItems[0].Count);
            
            // チェストを破壊して、採掘機の中にアイテムが残ることをチェックする
            worldBlockDatastore.RemoveBlock(chestBlockPos, BlockRemoveReason.ManualRemove);
            
            //鉱石2個分の採掘時間待機（tick数で制御）
            // Wait for two ore mining time (controlled by tick count)
            var waitTicks2 = (int)(miningTicks * 2 + 1);
            for (var i = 0; i < waitTicks2; i++) GameUpdater.RunFrames(1);
            
            //鉱石2個が残っているかチェック
            var outputSlot = miner.GetComponent<VanillaMinerProcessorComponent>().InventoryItems[0];
            Assert.AreEqual(miningItemId, outputSlot.Id);
            Assert.AreEqual(2, outputSlot.Count);
            
            
            // チェストを再度設置して、アイテムがアウトプットされていることを確認する
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, chestBlockPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out chestBlock);
            chestComponent = chestBlock.GetComponent<VanillaChestComponent>();
            
            //コネクターにアイテムを入れるためのアップデート
            GameUpdater.RunFrames(1);
            
            //アイテムがさらに2個入っているかチェック
            Assert.AreEqual(miningItemId, chestComponent.InventoryItems[0].Id);
            Assert.AreEqual(2, chestComponent.InventoryItems[0].Count);
        }
        
        // 原点セルは鉱脈AABBの外だが、多セル採掘機の底面フットプリントは鉱脈にXZで重なる配置を検証する
        // Verify a multi-cell miner whose origin cell sits outside the vein AABB but whose footprint overlaps it in XZ
        [Test]
        public void 原点は鉱脈の外でも底面フットプリントが重なれば採掘対象になる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var (vein, foundPos) = GetItemMapVein();

            // OffsetDrillMinerId（blockSize 2,1,3・North）の原点をvein最小X-1に置き、原点セルは鉱脈の外だが底面(x方向2セル)が鉱脈へ食い込むようにする
            // Place OffsetDrillMinerId (blockSize 2,1,3, North) so the origin cell misses the vein but its 2-cell-wide footprint reaches into it
            var originPos = new Vector3Int(vein.VeinRangeMin.x - 1, foundPos.y, foundPos.z);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            Assert.IsTrue(worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.OffsetDrillMinerId, originPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var minerBlock));

            var minerComponent = minerBlock.GetComponent<VanillaMinerProcessorComponent>();
            var miningItems = (List<IItemStack>)typeof(VanillaMinerProcessorComponent).GetField("_miningItems", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(minerComponent);
            var miningTicks = (uint)typeof(VanillaMinerProcessorComponent).GetField("_defaultMiningTicks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(minerComponent);

            Assert.AreEqual(1, miningItems.Count);
            Assert.AreEqual(vein.VeinItemId, miningItems[0].Id);
            Assert.Greater(miningTicks, 0u);
        }

        // mineSettingsに無い鉱脈の上では何も掘らない（採掘時間0のまま毎tick産出する無限増殖の再発防止）
        // A vein missing from mineSettings yields nothing (guards the every-tick yield with a zero mining time)
        [Test]
        public void mineSettingsに無い鉱脈の上では採掘対象が空になる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var unmineableVein = ServerContext.ItemMapVeinDatastore.Veins.First(v => v.VeinGuid == new Guid("11111111-0000-0000-0000-000000000004"));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            Assert.IsTrue(worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricMinerId, unmineableVein.VeinRangeMin, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var minerBlock));

            var minerComponent = minerBlock.GetComponent<VanillaMinerProcessorComponent>();
            var miningItems = (List<IItemStack>)typeof(VanillaMinerProcessorComponent).GetField("_miningItems", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(minerComponent);

            Assert.AreEqual(0, miningItems.Count);
        }

        public static (IItemMapVein mapVein, Vector3Int pos) GetItemMapVein()
        {
            var pos = new Vector3Int(0, 0);
            for (var i = 0; i < 500; i++)
            for (var j = 0; j < 500; j++)
            {
                List<IItemMapVein> veins = ServerContext.ItemMapVeinDatastore.GetVeinsContainingCell(new Vector3Int(i, j));
                if (veins.Count == 0) continue;
                
                return (veins[0], new Vector3Int(i, j));
            }
            
            return (null, pos);
        }
    }
}
