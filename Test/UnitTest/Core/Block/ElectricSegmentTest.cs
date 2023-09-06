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

            var electric = new BlockElectricConsumer(100, 0);
            var generate = new TestPowerGenerator(100, 0);

            segment.AddGenerator(generate);
            segment.AddBlockElectric(electric);
            GameUpdater.Update();
            Assert.AreEqual(100, electric.NowPower);

            segment.RemoveGenerator(generate);
            GameUpdater.Update();
            Assert.AreEqual(0, electric.NowPower);

            var electric2 = new BlockElectricConsumer(300, 1);
            segment.AddGenerator(generate);
            segment.AddBlockElectric(electric2);
            GameUpdater.Update();
            Assert.AreEqual(25, electric.NowPower);
            Assert.AreEqual(75, electric2.NowPower);

            segment.RemoveBlockElectric(electric);
            GameUpdater.Update();
            Assert.AreEqual(25, electric.NowPower);
            Assert.AreEqual(100, electric2.NowPower);
        }
    }

    class BlockElectricConsumer : IBlockElectricConsumer
    {
        public int EntityId { get; }
        public int RequestEnergy　{ get; }
        public int NowPower;
        

        public BlockElectricConsumer(int requestPower,int entityId)
        {
            EntityId = entityId;
            RequestEnergy = requestPower;
        }
        public void SupplyEnergy(int power)
        {
            NowPower = power;
        }
    }
}