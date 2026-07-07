using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 電気系ブロックのパラメータからワイヤー端点仕様を取り出す
    /// Extracts wire endpoint spec from an electric block param
    /// </summary>
    public static class ElectricWireBlockParamResolver
    {
        public static bool TryGetWireParam(IBlockParam blockParam, out int maxWireConnectionCount, out float maxWireLength)
        {
            switch (blockParam)
            {
                case ElectricPoleBlockParam pole:
                    maxWireConnectionCount = pole.MaxWireConnectionCount;
                    maxWireLength = pole.MaxWireLength;
                    return true;
                case ElectricMachineBlockParam machine:
                    maxWireConnectionCount = machine.MaxWireConnectionCount;
                    maxWireLength = machine.MaxWireLength;
                    return true;
                case ElectricGeneratorBlockParam generator:
                    maxWireConnectionCount = generator.MaxWireConnectionCount;
                    maxWireLength = generator.MaxWireLength;
                    return true;
                case ElectricMinerBlockParam miner:
                    maxWireConnectionCount = miner.MaxWireConnectionCount;
                    maxWireLength = miner.MaxWireLength;
                    return true;
                case ElectricPumpBlockParam pump:
                    maxWireConnectionCount = pump.MaxWireConnectionCount;
                    maxWireLength = pump.MaxWireLength;
                    return true;
                case GearToElectricGeneratorBlockParam gearToElectric:
                    maxWireConnectionCount = gearToElectric.MaxWireConnectionCount;
                    maxWireLength = gearToElectric.MaxWireLength;
                    return true;
                case ElectricToGearGeneratorBlockParam electricToGear:
                    maxWireConnectionCount = electricToGear.MaxWireConnectionCount;
                    maxWireLength = electricToGear.MaxWireLength;
                    return true;
                default:
                    // 電気系以外のブロックパラメータには対応しない
                    // Not an electric block param
                    maxWireConnectionCount = 0;
                    maxWireLength = 0;
                    return false;
            }
        }
    }
}
