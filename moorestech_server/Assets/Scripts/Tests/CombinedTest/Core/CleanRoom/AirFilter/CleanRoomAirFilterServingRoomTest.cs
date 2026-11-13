using Core.Update;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom.AirFilter
{
    public class CleanRoomAirFilterServingRoomTest
    {
        // 基準部屋の壁のうち、ハッチ・ドア以外の1枚
        // One wall block of the base room that is neither the hatch nor the door
        private static readonly Vector3Int SealWallPosition = new(1, 1, 0);

        [Test]
        public void OutsideRoomFilterDoesNotWearTest()
        {
            CleanRoomHatchTest.CreateServer();
            var block = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0));
            CleanRoomAirFilterTest.LoadFilterItems(block, 10);

            // 部屋の外では満電60秒でも減らず、寿命も進まない
            // Outside any room, sixty seconds at full power neither consume a filter nor advance the life
            CleanRoomAirFilterTest.TickWithPower(block, 100f, (int)GameUpdater.SecondsToTicks(60));
            Assert.AreEqual(10, CleanRoomAirFilterConsumptionTest.FilterCount(block));
            Assert.AreEqual(1d, CleanRoomAirFilterConsumptionTest.RemainingRatio(block), 1e-9);
        }

        [Test]
        public void BrokenSealStopsConsumptionAndResealResumesTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var block = CleanRoomAirFilterTest.BuildBaseRoom();
            CleanRoomAirFilterTest.LoadFilterItems(block, 10);
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out _));

            // 部屋の中で満電12秒なら1個減る
            // Twelve seconds at full power inside the room consume one filter
            CleanRoomAirFilterTest.TickWithPower(block, 100f, (int)GameUpdater.SecondsToTicks(12));
            Assert.AreEqual(9, CleanRoomAirFilterConsumptionTest.FilterCount(block));

            // 壁を1枚壊して部屋が消えたら、その後60秒は数も残り寿命も変わらない
            // Once a removed wall dissolves the room, sixty more seconds change neither the count nor the remaining life
            ServerContext.WorldBlockDatastore.RemoveBlock(SealWallPosition, BlockRemoveReason.ManualRemove);
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            Assert.IsFalse(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out _));
            var ratioAfterBreak = CleanRoomAirFilterConsumptionTest.RemainingRatio(block);
            for (var i = 0; i < GameUpdater.SecondsToTicks(60); i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(9, CleanRoomAirFilterConsumptionTest.FilterCount(block));
            Assert.AreEqual(ratioAfterBreak, CleanRoomAirFilterConsumptionTest.RemainingRatio(block), 1e-9);

            // 壁を戻して部屋が再成立すると、12秒でまた1個減る
            // Restoring the wall re-forms the room, and twelve seconds consume one more filter
            CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.CleanRoomWallId, SealWallPosition);
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out _));
            for (var i = 0; i < GameUpdater.SecondsToTicks(12); i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(8, CleanRoomAirFilterConsumptionTest.FilterCount(block));
        }

        [Test]
        public void RemovingServingPurifierKeepsTickingTest()
        {
            var block = CleanRoomAirFilterConsumptionTest.BuildServingPurifier(10);
            CleanRoomAirFilterTest.TickWithPower(block, 100f, 2);

            // 在室中の清浄機を撤去しても、次の判定tickで撤去済みへ在室解除を送らず進む
            // Removing a serving purifier does not push the leave notice to the removed block on later ticks
            ServerContext.WorldBlockDatastore.RemoveBlock(block.BlockPositionInfo.OriginalPos, BlockRemoveReason.ManualRemove);
            Assert.DoesNotThrow(() =>
            {
                for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            });
        }
    }
}
