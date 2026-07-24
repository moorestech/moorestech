using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 電気系ブロックのパラメータからワイヤー端点仕様を取り出す
    /// Extracts wire endpoint spec from an electric block param
    /// </summary>
    public static class ElectricWireBlockParamResolver
    {
        /// <summary>
        /// 電気系ブロックのパラメータから接続数上限・範囲プロファイル・電柱かどうかを取り出す
        /// Extract connection limit, range profile and pole-ness from an electric block param
        /// </summary>
        public static bool TryGetWireRangeParam(IBlockParam blockParam, out int maxWireConnectionCount, out ConnectionRangeProfile rangeProfile, out bool isPole)
        {
            switch (blockParam)
            {
                case ElectricPoleBlockParam pole:
                    maxWireConnectionCount = pole.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreatePole(pole);
                    isPole = true;
                    return true;
                case ElectricMachineBlockParam machine:
                    maxWireConnectionCount = machine.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(machine.ConnectionRange, machine.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case ElectricGeneratorBlockParam generator:
                    maxWireConnectionCount = generator.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(generator.ConnectionRange, generator.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case ElectricMinerBlockParam miner:
                    maxWireConnectionCount = miner.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(miner.ConnectionRange, miner.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case ElectricPumpBlockParam pump:
                    maxWireConnectionCount = pump.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(pump.ConnectionRange, pump.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case GearToElectricGeneratorBlockParam gearToElectric:
                    maxWireConnectionCount = gearToElectric.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(gearToElectric.ConnectionRange, gearToElectric.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case ElectricToGearGeneratorBlockParam electricToGear:
                    maxWireConnectionCount = electricToGear.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(electricToGear.ConnectionRange, electricToGear.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case CleanRoomAirFilterBlockParam airFilter:
                    maxWireConnectionCount = airFilter.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(airFilter.ConnectionRange, airFilter.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case CleanRoomMachineBlockParam cleanRoomMachine:
                    maxWireConnectionCount = cleanRoomMachine.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(cleanRoomMachine.ConnectionRange, cleanRoomMachine.ConnectionHeightRange);
                    isPole = false;
                    return true;
                default:
                    // 電気系以外のブロックパラメータには対応しない
                    // Not an electric block param
                    maxWireConnectionCount = 0;
                    rangeProfile = default;
                    isPole = false;
                    return false;
            }
        }
    }
}
