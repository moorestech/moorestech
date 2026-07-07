using Game.Block.Interface;
using Game.EnergySystem;

namespace Tests.UnitTest.Game
{
    /// <summary>
    /// FakeWireConnectorが持つ電力役割の最小実装群。グラフ構造の検証にのみ使うため挙動は空実装
    /// Minimal electric role implementations held by FakeWireConnector; behavior is a no-op since only graph structure is under test
    /// </summary>
    public class FakeElectricTransformer : IElectricTransformer
    {
        public FakeElectricTransformer(BlockInstanceId blockInstanceId)
        {
            BlockInstanceId = blockInstanceId;
        }

        public BlockInstanceId BlockInstanceId { get; }
        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }

    public class FakeElectricGenerator : IElectricGenerator
    {
        public FakeElectricGenerator(BlockInstanceId blockInstanceId)
        {
            BlockInstanceId = blockInstanceId;
        }

        public BlockInstanceId BlockInstanceId { get; }
        public bool IsDestroy { get; private set; }

        public ElectricPower OutputEnergy()
        {
            return new ElectricPower(0);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }

    public class FakeElectricConsumer : IElectricConsumer
    {
        public FakeElectricConsumer(BlockInstanceId blockInstanceId)
        {
            BlockInstanceId = blockInstanceId;
        }

        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy => new(0);
        public bool IsDestroy { get; private set; }

        public void SupplyEnergy(ElectricPower power)
        {
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
