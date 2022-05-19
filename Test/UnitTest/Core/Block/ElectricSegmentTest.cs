using Core.Electric;
using Core.Update;
using NUnit.Framework;
using Test.Module;

namespace Test.UnitTest.Core.Block
{
    public class ElectricSegmentTest
    {
        [Test]
        public void ElectricEnergyTest()
        {
            var segment = new ElectricSegment();

            var electric = new BlockElectric(100, 0);
            var generate = new TestPowerGenerator(100, 0);

            segment.AddGenerator(generate);
            segment.AddBlockElectric(electric);
            GameUpdate.Update();
            Assert.AreEqual(100, electric.NowPower);

            segment.RemoveGenerator(generate);
            GameUpdate.Update();
            Assert.AreEqual(0, electric.NowPower);

            var electric2 = new BlockElectric(300, 1);
            segment.AddGenerator(generate);
            segment.AddBlockElectric(electric2);
            GameUpdate.Update();
            Assert.AreEqual(25, electric.NowPower);
            Assert.AreEqual(75, electric2.NowPower);

            segment.RemoveBlockElectric(electric);
            GameUpdate.Update();
            Assert.AreEqual(25, electric.NowPower);
            Assert.AreEqual(100, electric2.NowPower);
        }
    }

    class BlockElectric : IBlockElectric
    {
        public int EntityId { get; }
        public int RequestPower　{ get; }
        public int NowPower;
        

        public BlockElectric(int requestPower,int entityId)
        {
            EntityId = entityId;
            RequestPower = requestPower;
        }
        public void SupplyPower(int power)
        {
            NowPower = power;
        }
    }
}