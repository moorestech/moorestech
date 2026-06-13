using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomPuritySimulationTest
    {
        [Test]
        public void ThresholdMaster_LoadsFourRows_BestFirst()
        {
            // DIコンテナ生成で MasterHolder.Load が走る。
            // Creating the DI container loads MasterHolder.
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var master = MasterHolder.CleanRoomThresholdMaster;
            Assert.AreEqual(4, master.Rows.Count);
            Assert.AreEqual(4, master.OutThresholdIndex);

            // 行0が最良（A相当）。値はバランス確定書§1。
            // Row 0 is the cleanest tier; values from balance §1.
            Assert.AreEqual(10.0, master.Rows[0].MaxConcentration, 1e-9);
            Assert.AreEqual(0.0167, master.Rows[0].RequiredAirChangeRate, 1e-9);
            Assert.AreEqual(1000.0, master.Rows[3].MaxConcentration, 1e-9);
            Assert.AreEqual(0.0014, master.Rows[3].RequiredAirChangeRate, 1e-9);
        }
    }
}
