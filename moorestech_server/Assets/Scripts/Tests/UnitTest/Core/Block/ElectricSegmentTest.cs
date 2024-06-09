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
            Assert.AreEqual(100, electric.CurrentPower.AsPrimitive());
            
            segment.RemoveGenerator(generate);
            GameUpdater.UpdateWithWait();
            Assert.AreEqual(0, electric.CurrentPower.AsPrimitive());
            
            var electric2 = new BlockElectricConsumer(300, new BlockInstanceId(1));
            segment.AddGenerator(generate);
            segment.AddEnergyConsumer(electric2);
            GameUpdater.UpdateWithWait();
            Assert.AreEqual(25, electric.CurrentPower.AsPrimitive());
            Assert.AreEqual(75, electric2.CurrentPower.AsPrimitive());
            
            segment.RemoveEnergyConsumer(electric);
            GameUpdater.UpdateWithWait();
            Assert.AreEqual(25, electric.CurrentPower.AsPrimitive());
            Assert.AreEqual(100, electric2.CurrentPower.AsPrimitive());
        }
    }
    
    internal class BlockElectricConsumer : IElectricConsumer
    {
        public ElectricPower CurrentPower;
        
        
        public BlockElectricConsumer(int requestPower, BlockInstanceId blockInstanceId)
        {
            BlockInstanceId = blockInstanceId;
            RequestEnergy = requestPower;
        }
        
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy　{ get; }
        
        public void SupplyEnergy(ElectricPower power)
        {
            CurrentPower = power;
        }
        
        public bool IsDestroy { get; }
        
        public void Destroy()
        {
        }
    }
}