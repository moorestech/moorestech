using Core.Master;
using Game.Fluid;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class FluidContainerFormatterTest
    {
        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void SerializeAndDeserializeFluidContainerTest()
        {
            // 液体入りコンテナのラウンドトリップ検証
            // Round-trip verification of a container with fluid
            var fluidId = MasterHolder.FluidMaster.GetFluidId(Tests.CombinedTest.Core.FluidTest.FluidGuid);
            const double capacity = 100.0;
            const double amount = 42.5;

            var original = new FluidContainer(capacity);
            original.FluidId = fluidId;
            original.Amount = amount;
            original.PreviousSourceFluidContainers.Add(new FluidContainer(10.0));

            var bytes = MessagePack.MessagePackSerializer.Serialize(original);
            var deserialized = MessagePack.MessagePackSerializer.Deserialize<FluidContainer>(bytes);

            Assert.AreEqual(capacity, deserialized.Capacity);
            Assert.AreEqual(amount, deserialized.Amount);
            Assert.AreEqual(fluidId, deserialized.FluidId);
            Assert.AreEqual(0, deserialized.PreviousSourceFluidContainers.Count);
        }

        [Test]
        public void SerializeAndDeserializeEmptyFluidContainerTest()
        {
            // 空コンテナのラウンドトリップ検証
            // Round-trip verification of an empty container
            const double capacity = 200.0;

            var original = new FluidContainer(capacity);

            var bytes = MessagePack.MessagePackSerializer.Serialize(original);
            var deserialized = MessagePack.MessagePackSerializer.Deserialize<FluidContainer>(bytes);

            Assert.AreEqual(capacity, deserialized.Capacity);
            Assert.AreEqual(0, deserialized.Amount);
            Assert.AreEqual(FluidMaster.EmptyFluidId, deserialized.FluidId);
        }

        [Test]
        public void SerializeAndDeserializeNullFluidContainerTest()
        {
            // nullのラウンドトリップ検証
            // Round-trip verification of null
            var bytes = MessagePack.MessagePackSerializer.Serialize<FluidContainer>(null);
            var deserialized = MessagePack.MessagePackSerializer.Deserialize<FluidContainer>(bytes);

            Assert.IsNull(deserialized);
        }

        [Test]
        public void JsonRoundTripTest()
        {
            // JSON経由のラウンドトリップ検証（セーブデータと同じ経路）
            // Round-trip via JSON (same path as save data)
            var fluidId = MasterHolder.FluidMaster.GetFluidId(Tests.CombinedTest.Core.FluidTest.FluidGuid);
            const double capacity = 50.0;
            const double amount = 33.3;

            var original = new FluidContainer(capacity);
            original.FluidId = fluidId;
            original.Amount = amount;

            var json = MessagePack.MessagePackSerializer.ConvertToJson(MessagePack.MessagePackSerializer.Serialize(original));
            var restored = MessagePack.MessagePackSerializer.Deserialize<FluidContainer>(MessagePack.MessagePackSerializer.ConvertFromJson(json));

            Assert.AreEqual(capacity, restored.Capacity);
            Assert.AreEqual(amount, restored.Amount, 0.001);
            Assert.AreEqual(fluidId, restored.FluidId);
        }
    }
}
