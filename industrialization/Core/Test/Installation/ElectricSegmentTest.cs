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

            var electric = new InstallationElectric(100);
            var generate = new TestPowerGenerator(100);
            
            segment.AddGenerator(generate);
            segment.AddInstallationElectric(electric);
            GameUpdate.Update();
            Assert.AreEqual(100, electric.nowPower);
        }
    }

    class InstallationElectric : IInstallationElectric
    {
        public int nowPower; 
        private int requestPower;
        public InstallationElectric(int request){requestPower = request;}
        public int RequestPower() {return requestPower;}
        public void SupplyPower(int power) {nowPower = power; }
    }
}