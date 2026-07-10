using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class BeltConveyorSpeedChangeTest
    {
        [Test]
        public void ProgressIsPreservedWhenSpeedChangesTest()
        {
            // 60%進行済みのアイテムを用意する
            // Prepare an item that has progressed 60 percent
            var item = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(1), null, null, 100)
            {
                RemainingTicks = 40,
            };

            // 速度変更後も進捗率60%が維持される
            // The 60 percent progress is preserved after the speed change
            item.UpdateTicksForSpeedChange(50);
            Assert.AreEqual(50u, item.TotalTicks);
            Assert.AreEqual(20u, item.RemainingTicks);
        }

        [Test]
        public void StoppedItemIsInitializedOnSpeedRecoveryTest()
        {
            // 停止中（TotalTicks=uint.MaxValue）に投入されたアイテムを用意する
            // Prepare an item inserted while the belt was stopped
            var item = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(1), null, null, uint.MaxValue);

            // 復帰時は新しい搬送時間で進捗0から開始する
            // On recovery the item starts from zero progress with the new transit time
            item.UpdateTicksForSpeedChange(80);
            Assert.AreEqual(80u, item.TotalTicks);
            Assert.AreEqual(80u, item.RemainingTicks);
        }

        [Test]
        public void SameSpeedDoesNotChangeTicksTest()
        {
            // 同一速度なら進捗はそのまま維持される
            // Identical speed keeps the current progress untouched
            var item = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(1), null, null, 100)
            {
                RemainingTicks = 70,
            };

            item.UpdateTicksForSpeedChange(100);
            Assert.AreEqual(100u, item.TotalTicks);
            Assert.AreEqual(70u, item.RemainingTicks);
        }
    }
}
