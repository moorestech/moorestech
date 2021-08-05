using industrialization.Core.Electric;
using industrialization.Core.GameSystem;
using NUnit.Framework;

namespace industrialization.Core.Test.Installation
{
    public class ElectricSegmentTest
    {
        //TODO ここをもっと書く
        [Test]
        public void ElectricEnergyTest()
        {
            var segment = new ElectricSegment();

            var electric = new InstallationElectric(100,0);
            var generate = new TestPowerGenerator(100,0);
            
            segment.AddGenerator(generate);
            segment.AddInstallationElectric(electric);
            GameUpdate.Update();
            Assert.AreEqual(100, electric.nowPower);
            
            segment.RemoveGenerator(generate);
            GameUpdate.Update();
            Assert.AreEqual(0, electric.nowPower);  
            
            var electric2 = new InstallationElectric(300,1);
            segment.AddGenerator(generate);
            segment.AddInstallationElectric(electric2);
            GameUpdate.Update();
            Assert.AreEqual(25, electric.nowPower);
            Assert.AreEqual(75, electric2.nowPower);
            
            segment.RemoveInstallationElectric(electric);
            GameUpdate.Update();
            Assert.AreEqual(25, electric.nowPower);
            Assert.AreEqual(100, electric2.nowPower);
        }
    }

    class InstallationElectric : IInstallationElectric
    {
        public int nowPower; 
        private int requestPower;
        private int id;
        public InstallationElectric(int request,int id){
            requestPower = request;
            this.id = id;
        }
        public int RequestPower() {return requestPower;}
        public void SupplyPower(int power) {nowPower = power; }
        public int GetIntId(){return id;}
    }
}