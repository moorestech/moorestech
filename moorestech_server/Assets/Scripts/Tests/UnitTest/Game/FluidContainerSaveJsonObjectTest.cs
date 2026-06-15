using Core.Master;
using Game.Fluid;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class FluidContainerSaveJsonObjectTest
    {
        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void JsonRoundTripWithFluidTest()
        {
            // 液体入りコンテナのJSONラウンドトリップ検証（セーブデータと同じ経路）
            // Round-trip a container with fluid via JSON (same path as save data)
            var fluidId = MasterHolder.FluidMaster.GetFluidId(Tests.CombinedTest.Core.FluidTest.FluidGuid);
            const double capacity = 100.0;
            const double amount = 42.5;

            var original = new FluidContainer(capacity);
            original.FluidId = fluidId;
            original.Amount = amount;

            var json = JsonConvert.SerializeObject(new FluidContainerSaveJsonObject(original));
            // 容量はマスタ由来なので復元時に渡す
            // Capacity is master-derived, so it is supplied at restore time
            var restored = JsonConvert.DeserializeObject<FluidContainerSaveJsonObject>(json).ToFluidContainer(capacity);

            Assert.AreEqual(capacity, restored.Capacity);
            Assert.AreEqual(amount, restored.Amount, 0.001);
            Assert.AreEqual(fluidId, restored.FluidId);
        }

        [Test]
        public void JsonRoundTripEmptyFluidTest()
        {
            // 空コンテナのJSONラウンドトリップ検証（FluidGuidはGuid.Emptyで保存される）
            // Round-trip an empty container via JSON (FluidGuid is persisted as Guid.Empty)
            const double capacity = 200.0;
            var original = new FluidContainer(capacity);

            var json = JsonConvert.SerializeObject(new FluidContainerSaveJsonObject(original));
            var restored = JsonConvert.DeserializeObject<FluidContainerSaveJsonObject>(json).ToFluidContainer(capacity);

            Assert.AreEqual(capacity, restored.Capacity);
            Assert.AreEqual(0, restored.Amount);
            Assert.AreEqual(FluidMaster.EmptyFluidId, restored.FluidId);
        }
    }
}
