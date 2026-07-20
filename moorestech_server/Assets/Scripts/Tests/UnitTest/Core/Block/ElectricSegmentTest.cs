using Game.Block.Interface;
using Game.EnergySystem;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util;

namespace Tests.UnitTest.Core.Block
{
    public class ElectricSegmentTest
    {
        [Test]
        public void ElectricEnergyTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var electric = new BlockElectricConsumer(new ElectricPower(100), new BlockInstanceId(0));
            var generate = new TestElectricGenerator(new ElectricPower(100), new BlockInstanceId(0));

            // 供給=需要のとき供給率1で全量が届く
            // Full demand is met at rate 1 when supply equals demand
            var balancedSegment = ElectricNetworkReflectionTestUtil.CreateSegment();
            ElectricNetworkReflectionTestUtil.AddGenerator(balancedSegment, generate);
            ElectricNetworkReflectionTestUtil.AddEnergyConsumer(balancedSegment, electric);
            var statistics = ElectricNetworkReflectionTestUtil.SettleTick(balancedSegment);
            Assert.AreEqual(1f, statistics.PowerRate);
            Assert.AreEqual(100, electric.RequestEnergy.AsPrimitive() * statistics.PowerRate);

            // 発電機がない区画は供給率0で自然停止する
            // A segment without a generator settles at rate 0 and the consumer naturally stops
            var consumerOnlySegment = ElectricNetworkReflectionTestUtil.CreateSegment();
            ElectricNetworkReflectionTestUtil.AddEnergyConsumer(consumerOnlySegment, electric);
            statistics = ElectricNetworkReflectionTestUtil.SettleTick(consumerOnlySegment);
            Assert.AreEqual(0f, statistics.PowerRate);

            // 需要400に対して供給100なら供給率0.25で按分される
            // With demand 400 against supply 100 the rate settles at 0.25 and power is shared proportionally
            var electric2 = new BlockElectricConsumer(new ElectricPower(300), new BlockInstanceId(1));
            var sharedSegment = ElectricNetworkReflectionTestUtil.CreateSegment();
            ElectricNetworkReflectionTestUtil.AddGenerator(sharedSegment, generate);
            ElectricNetworkReflectionTestUtil.AddEnergyConsumer(sharedSegment, electric);
            ElectricNetworkReflectionTestUtil.AddEnergyConsumer(sharedSegment, electric2);
            statistics = ElectricNetworkReflectionTestUtil.SettleTick(sharedSegment);
            Assert.AreEqual(0.25f, statistics.PowerRate);
            Assert.AreEqual(25, electric.RequestEnergy.AsPrimitive() * statistics.PowerRate);
            Assert.AreEqual(75, electric2.RequestEnergy.AsPrimitive() * statistics.PowerRate);

            // 需要300だけなら供給100で供給率1/3になる
            // A demand of 300 against supply 100 settles at a one-third rate
            var singleConsumerSegment = ElectricNetworkReflectionTestUtil.CreateSegment();
            ElectricNetworkReflectionTestUtil.AddGenerator(singleConsumerSegment, generate);
            ElectricNetworkReflectionTestUtil.AddEnergyConsumer(singleConsumerSegment, electric2);
            statistics = ElectricNetworkReflectionTestUtil.SettleTick(singleConsumerSegment);
            Assert.AreEqual(100f / 300f, statistics.PowerRate);
            Assert.AreEqual(100, electric2.RequestEnergy.AsPrimitive() * statistics.PowerRate, 0.001f);

            // 需要0のセグメントは供給率1として確定する
            // A segment with zero demand settles at rate 1
            var noDemandSegment = ElectricNetworkReflectionTestUtil.CreateSegment();
            ElectricNetworkReflectionTestUtil.AddGenerator(noDemandSegment, generate);
            statistics = ElectricNetworkReflectionTestUtil.SettleTick(noDemandSegment);
            Assert.AreEqual(1f, statistics.PowerRate);
            Assert.AreEqual(0, statistics.ConsumerCount);
        }
    }

    internal class BlockElectricConsumer : IElectricConsumer
    {
        public BlockElectricConsumer(ElectricPower requestPower, BlockInstanceId blockInstanceId)
        {
            BlockInstanceId = blockInstanceId;
            RequestEnergy = requestPower;
        }

        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy　{ get; }

        public bool IsDestroy { get; }

        public void Destroy()
        {
        }
    }
}
