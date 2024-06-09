using Core.Update;
using Game.Block.Interface;
using Game.EnergySystem;
using NUnit.Framework;
using Tests.Module;

namespace Tests.UnitTest.Core.Block
{
    public class ElectricSegmentTest
    {
        [Test]
        public void ElectricEnergyTest()
        {
            GameUpdater.ResetUpdate();
            var segment = new EnergySegment();
            
            var electric = new BlockElectricConsumer(100, new BlockInstanceId(0));
            var generate = new TestElectricGenerator(100, new BlockInstanceId(0));
            
            segment.AddGenerator(generate);
            segment.AddEnergyConsumer(electric);
            GameUpdater.UpdateWithWait();
            Assert.AreEqual(100, electric.NowPower);
            
            segment.RemoveGenerator(generate);
            GameUpdater.UpdateWithWait();
            Assert.AreEqual(0, electric.NowPower);
            
            var electric2 = new BlockElectricConsumer(300, new BlockInstanceId(1));
            segment.AddGenerator(generate);
            segment.AddEnergyConsumer(electric2);
            GameUpdater.UpdateWithWait();
            Assert.AreEqual(25, electric.NowPower);
            Assert.AreEqual(75, electric2.NowPower);
            
            segment.RemoveEnergyConsumer(electric);
            GameUpdater.UpdateWithWait();
            Assert.AreEqual(25, electric.NowPower);
            Assert.AreEqual(100, electric2.NowPower);
        }
    }
    
    internal class BlockElectricConsumer : IElectricConsumer
    {
        public int NowPower;
        
        
        public BlockElectricConsumer(int requestPower, BlockInstanceId blockInstanceId)
        {
            BlockInstanceId = blockInstanceId;
            RequestEnergy = requestPower;
        }
        
        public BlockInstanceId BlockInstanceId { get; }
        public float RequestEnergy　{ get; }
        
        public void SupplyEnergy(int power)
        {
            NowPower = power;
        }
        
        public bool IsDestroy { get; }
        
        public void Destroy()
        {
        }
    }
}