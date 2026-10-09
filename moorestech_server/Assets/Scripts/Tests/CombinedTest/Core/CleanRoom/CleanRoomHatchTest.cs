using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

using Tests.Util;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomHatchTest
    {
        [Test]
        public void SealedBoxWithHatchesFormsRoomTest()
        {
            var datastore = CreateServer();

            // 壁2枚をアイテムハッチとパイプハッチへ置き換えても密閉は維持される
            // Replacing two walls with item/pipe hatches keeps the box sealed
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            ReplaceBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(1, 1, 0));
            ReplaceBlock(ForUnitTestModBlockId.CleanRoomPipeHatchId, new Vector3Int(1, 1, 2));

            GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out var room));
            Assert.AreEqual(1, room.Volume);
        }

        [Test]
        public void BeltToHatchToChestTransportTest()
        {
            CreateServer();

            // 搬入チェスト→ベルト→ハッチ→チェストを北向きで直列に接続する
            // Chain source chest -> belt -> hatch -> chest facing north in a straight line
            var source = PlaceBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1)).GetComponent<VanillaChestComponent>();
            PlaceBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0));
            PlaceBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(0, 0, 1));
            var chestComponent = PlaceBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 2)).GetComponent<VanillaChestComponent>();
            source.SetItem(0, ServerContext.ItemStackFactory.Create(new ItemId(1), 1));

            // ベルト1マス・速度6: 出口まで255で43tick目にハッチへ渡る。ハッチは次のtick44の自身の更新でチェストへ押し出す
            // One belt cell at speed 6: 255 to the exit, handed to the hatch on tick 43; the hatch pushes it into the chest during its own update on tick 44
            GameUpdater.RunFrames(43);
            Assert.AreEqual(0, chestComponent.InventoryItems[0].Count);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, chestComponent.InventoryItems[0].Id.AsPrimitive());
            Assert.AreEqual(1, chestComponent.InventoryItems[0].Count);
        }

        [Test]
        public void FullTransitBufferRejectsInsertAndClogsBeltTest()
        {
            CreateServer();

            // 出力先の無いハッチへチェストからベルト経由で流し込み続けて詰まらせる
            // Keep feeding a hatch with no output target from a chest through a belt until the line clogs
            var sourceBlock = PlaceBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1));
            var source = sourceBlock.GetComponent<VanillaChestComponent>();
            var belt = PlaceBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0));
            var hatch = PlaceBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(0, 0, 1));
            var itemStackFactory = ServerContext.ItemStackFactory;
            source.SetItem(0, itemStackFactory.Create(new ItemId(1), 10));

            // 1マスのベルトは1個ずつ43tick周期で渡す。4個でハッチが埋まり、5個目(tick173投入)はtick215に出口で止まる
            // The one-cell belt hands one item every 43 ticks; four fill the hatch and the fifth (pushed on tick 173) parks at the exit on tick 215
            GameUpdater.RunFrames(300);

            // ハッチの中継バッファ4スタックが埋まり、以後の挿入は差し戻される
            // The 4-stack transit buffer is full, so further inserts bounce back
            var hatchInventory = hatch.GetComponent<IBlockInventory>();
            Assert.IsFalse(hatchInventory.InsertionCheck(new List<IItemStack> { itemStackFactory.Create(new ItemId(2), 1) }));
            var bounced = hatchInventory.InsertItem(itemStackFactory.Create(new ItemId(2), 3), InsertItemContext.Empty);
            Assert.AreEqual(2, bounced.Id.AsPrimitive());
            Assert.AreEqual(3, bounced.Count);

            // ハッチが受け取らないためベルト自体も満杯になり、チェストからの押し込みを拒否する
            // Because the hatch refuses items the belt itself fills up and rejects the chest's push
            var parked = BeltWorldTestUtil.ItemsOnSegmentAt(Vector3Int.zero);
            Assert.AreEqual(1, parked.Length);
            Assert.AreEqual(0, parked[0].DistanceToExit);
            Assert.AreEqual(5, source.GetItem(0).Count);
            var rejected = belt.GetComponent<IBlockInventory>().InsertItem(itemStackFactory.Create(new ItemId(1), 1), new InsertItemContext(sourceBlock.BlockInstanceId, null, null));
            Assert.AreEqual(1, rejected.Count);
        }

        [Test]
        public void ThroughputWindowDecaysToZeroTest()
        {
            CreateServer();

            var hatch = PlaceBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(0, 0, 0));
            PlaceBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 1));

            // 1個搬送した直後はレートが正になる
            // Right after one transfer the throughput must be positive
            var hatchInventory = hatch.GetComponent<IBlockInventory>();
            hatchInventory.InsertItem(ServerContext.ItemStackFactory.Create(new ItemId(1), 1), InsertItemContext.Empty);
            GameUpdater.UpdateOneTick();
            var hatchThroughput = hatch.GetComponent<ICleanRoomItemHatch>();
            Assert.Greater(hatchThroughput.RecentThroughputPerSecond, 0);

            // 20tick無搬送でリング窓が流れ切りレートは0に戻る
            // After 20 idle ticks the ring window drains and the rate returns to zero
            for (var i = 0; i < 20; i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, hatchThroughput.RecentThroughputPerSecond);
        }

        [Test]
        public void ItemHatchSaveLoadRestoresTransitBufferTest()
        {
            CreateServer();

            // 2スタック投入した状態をセーブし別インスタンスへ復元する
            // Save a hatch holding two stacks and restore it into a new instance
            var hatch = PlaceBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(0, 0, 0));
            var hatchInventory = hatch.GetComponent<IBlockInventory>();
            hatchInventory.InsertItem(ServerContext.ItemStackFactory.Create(new ItemId(1), 5), InsertItemContext.Empty);
            hatchInventory.InsertItem(ServerContext.ItemStackFactory.Create(new ItemId(2), 3), InsertItemContext.Empty);

            var saveComponent = hatch.GetComponent<IBlockSaveState>();
            var states = SaveLoadJsonTestHelper.ThroughJson(saveComponent.SaveKey, saveComponent.GetSaveState());

            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.CleanRoomItemHatchId).BlockGuid;
            var positionInfo = new BlockPositionInfo(new Vector3Int(10, 0, 10), BlockDirection.North, Vector3Int.one);
            var loaded = ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(int.MaxValue), states, positionInfo);

            var loadedInventory = loaded.GetComponent<IBlockInventory>();
            Assert.AreEqual(1, loadedInventory.GetItem(0).Id.AsPrimitive());
            Assert.AreEqual(5, loadedInventory.GetItem(0).Count);
            Assert.AreEqual(2, loadedInventory.GetItem(1).Id.AsPrimitive());
            Assert.AreEqual(3, loadedInventory.GetItem(1).Count);
        }

        #region TestHelper

        public static CleanRoomDatastore CreateServer()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return serviceProvider.GetService<CleanRoomDatastore>();
        }

        public static IBlock PlaceBlock(BlockId blockId, Vector3Int pos)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            return block;
        }

        public static IBlock ReplaceBlock(BlockId blockId, Vector3Int pos)
        {
            ServerContext.WorldBlockDatastore.RemoveBlock(pos, BlockRemoveReason.ManualRemove);
            return PlaceBlock(blockId, pos);
        }

        #endregion
    }
}
